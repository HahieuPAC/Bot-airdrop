using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using OpenQA.Selenium.Interactions;

class Program
{
    static IWebDriver? kiteDriver = null;
    static IWebDriver? chatGptDriver = null;
    static bool isRunning = true;
    static readonly string KITE_URL = "https://agents.testnet.gokite.ai/";
    static readonly string CHATGPT_URL = "https://chatgpt.com/c/67cdbd3e-3f7c-800c-b3db-f5047e8f4634";
    static readonly string METAMASK_PASSWORD = "H@trunghj3up@c112358";
    
    // Thêm hằng số cho đường dẫn profile
    static readonly string EDGE_USER_DATA_DIR = Path.Combine(
        Environment.GetEnvironmentVariable("USERPROFILE"),
        @"AppData\Local\Microsoft\Edge\User Data"
    );
    static readonly string CHATGPT_USER_DATA_DIR = Path.Combine(
        Environment.GetEnvironmentVariable("USERPROFILE"),
        @"AppData\Local\Microsoft\Edge\User Data ChatGPT"
    );

    static void Main()
    {
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            isRunning = false;
            CleanupAndExit();
            Environment.Exit(0);
        };

        try
        {
            Console.WriteLine("🚀 Đang khởi động chương trình...");
            
            KillAllEdgeProcesses();
            Thread.Sleep(2000);

            // Cấu hình riêng cho Kite
            var kiteOptions = ConfigureEdgeOptions(true);
            // Cấu hình riêng cho ChatGPT với thư mục riêng
            var chatGptOptions = ConfigureEdgeOptions(false);
            
            Console.WriteLine("Nhấn Ctrl+C để dừng chương trình...");

            // Mở Kite và xử lý Metamask
            Console.WriteLine("🌐 Đang mở Kite...");
            kiteDriver = new EdgeDriver(kiteOptions);
            kiteDriver.Navigate().GoToUrl(KITE_URL);
            HandleMetamask(kiteDriver);
            Console.WriteLine("✅ Đã mở Kite");

            // Mở ChatGPT trong cửa sổ mới
            Console.WriteLine("🤖 Đang mở ChatGPT...");
            Thread.Sleep(2000);
            
            try
            {
                chatGptDriver = new EdgeDriver(chatGptOptions);
                chatGptDriver.Navigate().GoToUrl(CHATGPT_URL);
                Console.WriteLine("✅ Đã mở ChatGPT");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi mở ChatGPT: {ex.Message}");
                Console.WriteLine("Đang thử lại...");
                Thread.Sleep(2000);
                
                if (chatGptDriver != null)
                {
                    try { chatGptDriver.Quit(); } catch { }
                }
                chatGptDriver = new EdgeDriver(chatGptOptions);
                chatGptDriver.Navigate().GoToUrl(CHATGPT_URL);
            }

            Console.WriteLine("✅ Đã mở tất cả các trang");

            while (isRunning)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi: {ex.Message}");
        }
        finally
        {
            CleanupAndExit();
        }
    }

    static EdgeOptions ConfigureEdgeOptions(bool isKite = true)
    {
        var options = new EdgeOptions();
        
        if (isKite)
        {
            // Kite sử dụng thư mục User Data gốc
            options.AddArgument($"--user-data-dir={EDGE_USER_DATA_DIR}");
            options.AddArgument("--profile-directory=Profile 1");
        }
        else
        {
            // Đảm bảo thư mục ChatGPT tồn tại
            EnsureChatGPTProfile();
            
            options.AddArgument($"--user-data-dir={CHATGPT_USER_DATA_DIR}");
            options.AddArgument("--profile-directory=Default");
        }
        
        options.AddArgument("--enable-extensions");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--disable-logging");
        options.AddArgument("--log-level=3");
        options.AddArgument("--silent");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        return options;
    }

    // Thêm phương thức để đảm bảo profile ChatGPT
    static void EnsureChatGPTProfile()
    {
        try
        {
            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(CHATGPT_USER_DATA_DIR))
            {
                Console.WriteLine("📁 Đang tạo profile mới cho ChatGPT...");
                Directory.CreateDirectory(CHATGPT_USER_DATA_DIR);

                // Copy các file cần thiết từ profile gốc (preferences, extensions, etc.)
                var defaultFiles = new[] { "Preferences", "Secure Preferences", "Local State" };
                foreach (var file in defaultFiles)
                {
                    var sourcePath = Path.Combine(EDGE_USER_DATA_DIR, file);
                    var destPath = Path.Combine(CHATGPT_USER_DATA_DIR, file);
                    if (File.Exists(sourcePath) && !File.Exists(destPath))
                    {
                        File.Copy(sourcePath, destPath);
                    }
                }

                Console.WriteLine("✅ Đã tạo profile ChatGPT thành công");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi tạo profile ChatGPT: {ex.Message}");
        }
    }

    static void HandleMetamask(IWebDriver driver)
    {
        try
        {
            Console.WriteLine("🦊 Đang đợi Metamask (timeout: 5 phút)...");
            DateTime endTime = DateTime.Now.AddMinutes(5);
            string mainWindow = driver.CurrentWindowHandle;
            string? metamaskWindow = null;

            // Tìm cửa sổ MetaMask
            while (DateTime.Now < endTime && metamaskWindow == null && isRunning)
            {
                var remaining = endTime - DateTime.Now;
                Console.WriteLine($"⏳ Còn {remaining.Minutes} phút {remaining.Seconds} giây để đợi Metamask...");

                var windows = driver.WindowHandles;
                Console.WriteLine($"🔍 Đang kiểm tra {windows.Count} cửa sổ...");

                foreach (string window in windows)
                {
                    if (!isRunning) return;

                    driver.SwitchTo().Window(window);
                    string title = driver.Title;
                    Console.WriteLine($"Kiểm tra cửa sổ: {title}");

                    if (title == "MetaMask")
                    {
                        Console.WriteLine("✅ Đã tìm thấy cửa sổ MetaMask!");
                        metamaskWindow = window;
                        
                        // Đợi 2 giây cho cửa sổ MetaMask load hoàn tất
                        Thread.Sleep(2000);
                        
                        // Tạo Actions để nhập mật khẩu
                        Console.WriteLine("✍️ Đang nhập mật khẩu...");
                        var actions = new Actions(driver);
                        actions.SendKeys(METAMASK_PASSWORD).Perform();
                        Thread.Sleep(1000);
                        
                        // Nhấn Enter
                        Console.WriteLine("🔓 Đang mở khóa...");
                        actions.SendKeys(Keys.Enter).Perform();
                        
                        // Chuyển về cửa sổ chính
                        driver.SwitchTo().Window(mainWindow);
                        Console.WriteLine("✅ Đã đăng nhập Metamask thành công");
                        return;
                    }
                }

                if (metamaskWindow == null && isRunning)
                {
                    Thread.Sleep(3000);
                }
            }

            if (!isRunning) return;

            if (metamaskWindow == null)
            {
                Console.WriteLine("❌ Không tìm thấy cửa sổ MetaMask!");
                if (isRunning)
                {
                    Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
                    Console.ReadLine();
                    HandleMetamask(driver);
                }
                return;
            }
        }
        catch (Exception ex)
        {
            if (!isRunning) return;
            Console.WriteLine($"❌ Lỗi không mong muốn: {ex.Message}");
            Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
            Console.ReadLine();
            if (isRunning) HandleMetamask(driver);
        }
    }

    static void KillAllEdgeProcesses()
    {
        try
        {
            Console.WriteLine("🔍 Đang kiểm tra và đóng các tiến trình Edge...");
            
            foreach (var process in Process.GetProcessesByName("msedgedriver"))
            {
                try
                {
                    process.Kill();
                }
                catch { }
            }

            foreach (var process in Process.GetProcessesByName("msedge"))
            {
                try
                {
                    process.Kill();
                }
                catch { }
            }

            Console.WriteLine("✅ Đã đóng tất cả các tiến trình Edge");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi đóng tiến trình Edge: {ex.Message}");
        }
    }

    static void CleanupAndExit()
    {
        Console.WriteLine("\n🛑 Đang dừng chương trình...");
        
        try
        {
            if (kiteDriver != null)
            {
                kiteDriver.Quit();
                kiteDriver = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi đóng Kite: {ex.Message}");
        }

        try
        {
            if (chatGptDriver != null)
            {
                chatGptDriver.Quit();
                chatGptDriver = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Lỗi khi đóng ChatGPT: {ex.Message}");
        }

        KillAllEdgeProcesses();
        Console.WriteLine("✅ Đã đóng tất cả trình duyệt.");
    }
}
