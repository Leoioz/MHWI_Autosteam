using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoSteamApp.Core;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using Keystroke.API;

namespace AutoSteamApp
{
    class Program
    {
        private const string ProcessName = "MonsterHunterWorld";
        private static volatile bool shouldStop = false;
        private static volatile bool shouldStart = false;

        private static Random rnd = new Random();
        private static KeystrokeAPI api = new KeystrokeAPI();
        private static readonly Dictionary<VirtualKeyCode, int> keyOrder = new Dictionary<VirtualKeyCode, int>()
        {
            { VirtualKeyCode.VK_A, 999 },
            { VirtualKeyCode.VK_W, 999 },
            { VirtualKeyCode.VK_D, 999 },
            { VirtualKeyCode.VK_Q, 999 },
            { VirtualKeyCode.VK_Z, 999 },
        };

        private static readonly Dictionary<int, List<int>> rndPatterns = new Dictionary<int, List<int>>()
        {
            { 0, new List<int> { 0, 1, 2 } },
            { 1, new List<int> { 1, 0, 2 } },
            { 2, new List<int> { 2, 0, 1 } },
            { 3, new List<int> { 0, 2, 1 } },
            { 4, new List<int> { 2, 1, 0 } },
            { 5, new List<int> { 1, 2, 0 } }
        };

        private static Process mhw;
        private static CancellationTokenSource ct = new CancellationTokenSource();
        private static bool IsCorrectVersion = true;
        private static bool IsSmartRun = false;

        static void Main(string[] args)
        {
            WriteMenu();

            HookKeyboardEvents();

            Startup();

            if (IsCorrectVersion)
            {
                DoWork(IsSmartRun); //读内存100%成功率
            }
            else
            {
                DoRandomWork();//random随机
            }
        }

        private static void WriteMenu()
        {
            Console.Title = $"当前游戏版本: ({Settings.SupportedGameVersion})";
            Console.WriteLine($"-------------------------------------------------------------------------------");
            Console.WriteLine($"开源工具修订免责声明：");
            Console.WriteLine($"本次修订基于https://github.com/AdiBorsos/AutoSteamworks；感谢作者AdiBorsos！");
            Console.WriteLine($"本次修订内容，将内存读取地址更新到最新15.22（数字号421470）的内存对应地址");
            Console.WriteLine($"其次把界面还有log输出改为简中，方便大家定位工具不能运行的原因");
            Console.WriteLine($"-------------------------------------------------------------------------------");
            Console.WriteLine("");
            Console.WriteLine($"工具读取当前游戏版本是:  {Settings.SupportedGameVersion},当前支持的版本是15.22（数字号421470）");

            Console.WriteLine(string.Empty);

            Console.WriteLine(
                string.Format(
                    "燃料默认设置为：把所有燃料消耗到0，才停止，如果你想改，参考redeme文档（内心OS：有end键结束其实懒得改呢）",
                    Settings.ShouldConsumeAllFuel ? "ALL the available" : "ONLY the Natural"));
            Console.WriteLine(string.Empty);

            WriteSeparator();
            Console.WriteLine($"通过下面的提示选择运行模式，如果按end键，程序会自动退出");
            Console.WriteLine($"如果按home键，建议大家先管理员模式启动工具，游戏设置为无边框模式，然后切换到游戏显示AWD按键状态，按下home后，等四五秒别乱动键盘！");
            WriteSeparator();
            Console.WriteLine($"按 '{((KeyCode)Settings.KeyCodeStart).ToString()}'键运行读取蒸汽机内存数据模式（100%成功率）");
            Console.WriteLine($"");
            Console.WriteLine($"按'{((KeyCode)Settings.KeyCodeStartRandom).ToString()}'键运行工具内置的数组（等价于工具帮你随便按）");
            Console.WriteLine($"");
            Console.WriteLine($"按'{((KeyCode)Settings.KeyCodeStop).ToString()}'结束工具");
            WriteSeparator();
            Console.WriteLine($"以上按键支持全局模式，所以大家自己要注意按键冲突");
        }

        private static void WriteSeparator()
        {
            Console.WriteLine($"--------------------------------------------------------------------------------------");
        }

        private static void Startup()
        {
            while (mhw == null && !ct.IsCancellationRequested)
            {
                mhw = GetMHW();//遍历找到{System.Diagnostics.Process (MonsterHunterWorld)}
                Thread.Sleep(1000);
            }

            while (!shouldStart && !ct.IsCancellationRequested)//dengdaikaishi
            {
                Thread.Sleep(1000);
            }

            if (mhw == null)
            {
                if (!mhw.MainWindowTitle.Contains(Settings.SupportedGameVersion))
                {
                    IsCorrectVersion = false;

                    var currentVersion = int.Parse(mhw.MainWindowTitle.Split('(')[1].Replace(")", ""));
                    Logger.LogError($"当前游戏版本: {Settings.SupportedGameVersion}. 这个版本({currentVersion})不匹配，如果遇到这个提示，有可能卡普空诈尸游戏刚更新，新版工具还没出");

                    if (IsSmartRun)
                    {
                        Logger.LogError($"恭喜你读取内存失败，具体原因可能是两种：1.没有使用管理员模式运行，工具没权限读内存数据；2.游戏更新版本了，内存地址发生变化如果不想手动按，可以选择F1键的随机模式");
                        Logger.LogError($"原作者在这个位置说：“选择F1，总比手动按舒服”，我认为选择太刀！选择成功！");

                        mhw = null;
                    }
                }
                else
                {
                    IsCorrectVersion = true;

                    if (!IsSmartRun)
                    {
                        Logger.LogError($"随机模式启动");
                        
                        return;
                    }
                }
            }
        }

        private static void DoRandomWork()
        {
            if (mhw != null && !ct.IsCancellationRequested)
            {
                InputSimulator sim = new InputSimulator();
                while (!shouldStop && !ct.IsCancellationRequested)
                {
                    List<KeyValuePair<VirtualKeyCode, int>> orderBytes = GetRandomSequence();

                    foreach (var item in orderBytes)
                    {
                        PressKey(sim, item.Key, true);
                    }

                    PressKey(sim, (VirtualKeyCode)Settings.KeyCutsceneSkip, true);

                    PressKey(sim, VirtualKeyCode.SPACE, true);
                }

                api.Dispose();
            }
        }

        private static List<KeyValuePair<VirtualKeyCode, int>> GetRandomSequence()
        {
            List<int> orderBytes = rndPatterns[rnd.Next(0, 5)];

            if (Settings.IsAzerty)
            {
                keyOrder[VirtualKeyCode.VK_Q] = orderBytes[0];   // Q
                keyOrder[VirtualKeyCode.VK_Z] = orderBytes[1];   // Z
                keyOrder[VirtualKeyCode.VK_D] = orderBytes[2];   // D
            }
            else
            {
                keyOrder[VirtualKeyCode.VK_A] = orderBytes[0];   // A
                keyOrder[VirtualKeyCode.VK_W] = orderBytes[1];   // W
                keyOrder[VirtualKeyCode.VK_D] = orderBytes[2];   // D
            }

            return keyOrder.OrderBy(x => x.Value).Take(3).ToList();
        }

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = WindowsApi.GetForegroundWindow();

            if (WindowsApi.GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }

            return null;
        }

        private static bool IsCurrentActiveMHW()
        {
            return mhw.MainWindowTitle == GetActiveWindowTitle();
        }

        private static void DoWork(bool isSmartRun = true)
        {
            if (mhw != null && !ct.IsCancellationRequested)
            {
                InputSimulator sim = new InputSimulator();

                SaveData sd = new SaveData(mhw, ct);

                ulong starter = Settings.Off_Base + Settings.Off_SteamworksCombo;

                var pointerAddress = MemoryHelper.Read<ulong>(mhw, starter);
                // offset the address
                var offset_Address = pointerAddress + 0x350;
                var offset_buttonPressState = offset_Address + 8;

                var oldFuelValue = sd.NaturalFuel + sd.StoredFuel;
                var fuelPerRound = 10;

                while (!shouldStop && !ct.IsCancellationRequested)
                {
                    // Logger.LogInfo($"Gauge Data {sd.SteamGauge}!");

                    // value of the offset address
                    List<KeyValuePair<VirtualKeyCode, int>> ordered = 
                        isSmartRun ? 
                            ExtractCorrectSequence(mhw, offset_Address) : 
                            GetRandomSequence();

                    if (ordered == null)
                    {
                        Logger.LogInfo("遇到这个提示原因是，启动这个工具比如按home键时，你游戏画面要切换到蒸汽机，并且按空格开始游戏，要看到三个AWD按键");

                        // try again..
                        continue;
                    }

                    int index = 0;
                    while (index < 3)
                    {
                        try
                        {
                            var before = MemoryHelper.Read<byte>(mhw, offset_buttonPressState);

                            var item = ordered[index];

                            byte after = before;
                            while (before == after && !ct.IsCancellationRequested)
                            {
                                PressKey(sim, item.Key);

                                after = MemoryHelper.Read<byte>(mhw, offset_buttonPressState);
                            }

                            index++;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"尝试按键下面按键-> {ex.Message}错误（一般大家不会遇到这个错误，只服务于开发严谨性）");
                        }
                    }

                    // Small work around to avoid blocking when running x10 fuel per sequence
                    if (oldFuelValue - sd.NaturalFuel - sd.StoredFuel == 100)
                    {
                        fuelPerRound = 100;
                    }
                    else
                    {
                        fuelPerRound = 10;
                    }

                    oldFuelValue = sd.NaturalFuel + sd.StoredFuel;

                    if (shouldStop)
                    {
                        break;
                    }

                    var currentState = MemoryHelper.Read<byte>(mhw, offset_buttonPressState);
                    while (currentState != (int)ButtonPressingState.BeginningOfSequence && !ct.IsCancellationRequested)
                    {
                        Thread.Sleep(50);

                        try
                        {
                            PressKey(sim, (VirtualKeyCode)Settings.KeyCutsceneSkip, true);

                            // no more fuel
                            if (currentState == (int)ButtonPressingState.EndOfGame)
                            {
                                if (sd.NaturalFuel + (sd.StoredFuel * (Settings.ShouldConsumeAllFuel ? 1 : 0)) < fuelPerRound)
                                {
                                    Logger.LogInfo(
                                        string.Format(
                                            "没有燃料了，请关闭",
                                            Settings.ShouldConsumeAllFuel == false ? "燃料 " : string.Empty));

                                    shouldStop = true;
                                    break;
                                }

                                if (sd.SteamGauge == 0)
                                {
                                    PressKey(sim, VirtualKeyCode.SPACE, true);
                                }
                            }

                            if (shouldStop)
                            {
                                break;
                            }

                            currentState = MemoryHelper.Read<byte>(mhw, offset_buttonPressState);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"尝试完成以下按键: {ex.Message}（一般不会遇到这个问题，只服务于开发严谨性）");
                        }
                    }
                }

                api.Dispose();
            }
        }

        private static List<KeyValuePair<VirtualKeyCode, int>> ExtractCorrectSequence(Process mhw, ulong offset_Address)
        {
            try
            {
                Thread.Sleep(rnd.Next((int)Settings.DelayBetweenCombo));

                var actualSequence = MemoryHelper.Read<int>(mhw, offset_Address);
                if (actualSequence == 0)
                {
                    // wait for init of Steamworks
                    return null;
                }

                var orderBytes = BitConverter.GetBytes(actualSequence);
                // Some shitty logic suggested by https://github.com/Geobryn which fixes the accuracy
                if (orderBytes[0] == 2 && orderBytes[1] == 0 && orderBytes[2] == 1)
                {
                    orderBytes[0] = 1;
                    orderBytes[1] = 2;
                    orderBytes[2] = 0;
                }
                else
                if (orderBytes[0] == 1 && orderBytes[1] == 2 && orderBytes[2] == 0)
                {
                    orderBytes[0] = 2;
                    orderBytes[1] = 0;
                    orderBytes[2] = 1;
                }

                if (Settings.IsAzerty)
                {
                    keyOrder[VirtualKeyCode.VK_Q] = int.Parse(((char)(orderBytes[0] + 0x30)).ToString());   // Q
                    keyOrder[VirtualKeyCode.VK_Z] = int.Parse(((char)(orderBytes[1] + 0x30)).ToString());   // Z
                    keyOrder[VirtualKeyCode.VK_D] = int.Parse(((char)(orderBytes[2] + 0x30)).ToString());   // D
                }
                else
                {
                    keyOrder[VirtualKeyCode.VK_A] = int.Parse(((char)(orderBytes[0] + 0x30)).ToString());   // A
                    keyOrder[VirtualKeyCode.VK_W] = int.Parse(((char)(orderBytes[1] + 0x30)).ToString());   // W
                    keyOrder[VirtualKeyCode.VK_D] = int.Parse(((char)(orderBytes[2] + 0x30)).ToString());   // D
                }

                var ordered = keyOrder.OrderBy(x => x.Value).ToList();
                Logger.LogInfo($"正在按 {string.Join(" -> ", ordered.Take(3).Select(x => x.Key.ToString()))}");

                return ordered;
            }
            catch (Exception ex)
            {
                Logger.LogError($"接下来按: {ex.Message}");

                return null;
            }
        }

        private static void PressKey(InputSimulator sim, VirtualKeyCode key, bool delay = false)
        {
            while (!IsCurrentActiveMHW())
            {
                Logger.LogInfo("正在等待游戏启动，或者请切换到游戏画面打开蒸汽机，并按空格，要看到AWD三个按钮");
            }

            Logger.LogInfo($"正在: {key}!");

            if (Settings.UseBackgroundKeyPress)
            {
                Logger.LogInfo($"作者原话：You cheeky bastard. This doesn't work yet ..Please switch the flag back.（不知道作者吐槽什么）");
                //mhw.WaitForInputIdle();
                //var keyMap = new Key((Messaging.VKeys)key);

                //keyMap.PressBackground(mhw.MainWindowHandle);
            }
            else
            {
                if (delay)
                {
                    sim.Keyboard.KeyDown(key);
                    sim.Keyboard.Sleep(100);
                    sim.Keyboard.KeyUp(key);

                    return;
                }

                sim.Keyboard.KeyPress(key);
            }
        }

        private static void HookKeyboardEvents()
        {
            Task.Run(() =>
            {
                api.CreateKeyboardHook((character) =>
                {
                    if (character.KeyCode == (KeyCode)Settings.KeyCodeStart)
                    {
                        shouldStart = true;
                        IsSmartRun = true;

                        Logger.LogInfo(string.Format("燃料类型消耗 >>{0}<< ", Settings.ShouldConsumeAllFuel ? "所有燃料模式" : "只是每日燃料"));
                    }

                    if (character.KeyCode == (KeyCode)Settings.KeyCodeStartRandom)
                    {
                        shouldStart = true;
                        IsSmartRun = false;

                        Logger.LogInfo(string.Format("燃料类型消耗 >>{0}<< fuel!", Settings.ShouldConsumeAllFuel ? "所有燃料模式" : "只是每日燃料"));
                    }

                    if (character.KeyCode == (KeyCode)Settings.KeyCodeStop)
                    {
                        ct.Cancel();

                        shouldStart = true;
                        shouldStop = true;

                        Logger.LogInfo($"停止读内存，结束程序");

                        Application.Exit();
                    }
                });

                Application.Run();
            });
        }

        private static Process GetMHW()
        {
            var processes = Process.GetProcesses();
            try
            {
                return processes.FirstOrDefault(p => p != null && p.ProcessName.Equals(ProcessName) && !p.HasExited);
            }
            catch
            {
                Logger.LogError($"没有找到 '{ProcessName}'，请启动游戏，并最好调整为无边框模式");
            }

            Logger.LogError($"游戏没启动，或者建议调整为无边框模式");

            return null;
        }
    }
}
