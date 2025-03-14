﻿using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Linq;
using OpenQA.Selenium.Interactions;
using System.Text.Json;

class Program
{
    static IWebDriver? kiteDriver = null;
    static IWebDriver? chatGptDriver = null;
    static bool isRunning = true;
    static readonly string KITE_URL = "https://agents.testnet.gokite.ai/";
    static readonly string CHATGPT_URL = "https://chatgpt.com/c/67cdbd3e-3f7c-800c-b3db-f5047e8f4634";
    static readonly string METAMASK_PASSWORD = "H@trunghj3up@c112358";
    
    // Thêm hằng số cho đường dẫn profile
    static readonly string BASE_EDGE_USER_DATA_DIR = GetEdgeUserDataDir();
    static readonly string CHATGPT_USER_DATA_DIR = Path.Combine(
        Path.GetDirectoryName(BASE_EDGE_USER_DATA_DIR) ?? "",
        "Edge",
        "User Data",
        "ChatGPT"
    ).Replace(
        Path.Combine("Edge", "Edge"),
        "Edge"
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
                Console.WriteLine("✅ Đã mở ChatGPT thành công!");
  
                Console.WriteLine("⌛ Đợi ChatGPT khởi động và tìm ô nhập văn bản...");
                var chatGptWait = new WebDriverWait(chatGptDriver, TimeSpan.FromMinutes(2));
  
                // Gửi câu hỏi đầu tiên cho ChatGPT và đợi câu trả lời
                try 
                {
                    // Tìm và gửi câu hỏi đầu tiên
                    var inputDiv = chatGptWait.Until(d => d.FindElement(By.XPath("//p[@data-placeholder='Ask anything']")));
                    if (inputDiv != null)
                    {
                        Console.WriteLine("✅ Đã tìm thấy ô nhập văn bản!");
                        
                        string firstMessage = "ask questions about the gokiteAI project, concise and only one line";
                        
                        // Focus và nhập text bằng JavaScript
                        IJavaScriptExecutor js = (IJavaScriptExecutor)chatGptDriver;
                        js.ExecuteScript(@"
                            arguments[0].focus();
                            arguments[0].innerHTML = arguments[1];
                        ", inputDiv, firstMessage);
                        
                        Thread.Sleep(500);
                        
                        // Gửi câu hỏi
                        var sendButton = chatGptWait.Until(d => d.FindElement(By.CssSelector("button[data-testid='send-button']")));
                        if (sendButton != null && sendButton.Enabled)
                        {
                            sendButton.Click();
                            Console.WriteLine("✅ Đã gửi câu hỏi đầu tiên cho ChatGPT!");
                            
                            // Đợi câu trả lời đầu tiên
                            Console.WriteLine("⌛ Đang đợi câu trả lời đầu tiên từ ChatGPT...");
                            var firstResponse = chatGptWait.Until(d => 
                                d.FindElement(By.XPath("(//div[contains(@class, \"markdown\")])[last()]")));

                            if (firstResponse != null)
                            {
                                Console.WriteLine("✅ Đã nhận được câu trả lời đầu tiên từ ChatGPT!");
                                Console.WriteLine("\n🤖 ChatGPT trả lời:");
                                Console.WriteLine("------------------------------------------");
                                Console.WriteLine(firstResponse.Text);
                                Console.WriteLine("------------------------------------------\n");
                                
                                // Đợi 2 giây trước khi bắt đầu vòng lặp chính
                                Thread.Sleep(2000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Lỗi khi xử lý câu hỏi đầu tiên: {ex.Message}");
                    Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để tiếp tục hoặc Ctrl+C để thoát...");
                    Console.ReadLine();
                }
  
                // Bắt đầu vòng lặp chính để xử lý hội thoại
                Console.WriteLine("\n🔄 Bắt đầu vòng lặp xử lý hội thoại...");
                int conversationCount = 0;
                const int MAX_CONVERSATIONS = 21;
  
                while (conversationCount < MAX_CONVERSATIONS)
                {
                    conversationCount++;
                    Console.WriteLine($"\n🔄 Lượt hội thoại thứ {conversationCount}/{MAX_CONVERSATIONS}");
  
                    // Kiểm tra textarea của ChatGPT trước
                    Console.WriteLine("🔍 Kiểm tra textarea của ChatGPT...");
                    Console.WriteLine("🔍 Tìm với XPath: //p[@data-placeholder='Ask anything']");
                    IWebElement? chatGptInput = null;
                    try
                    {
                        chatGptInput = chatGptDriver.FindElement(By.XPath("//p[@data-placeholder='Ask anything']"));
                        Console.WriteLine("✅ Đã tìm thấy textarea của ChatGPT!");
                        // Hiển thị thông tin về textarea để debug
                        Console.WriteLine($"📝 Class của textarea: {chatGptInput.GetAttribute("class")}");
                        Console.WriteLine($"📝 Placeholder: {chatGptInput.GetAttribute("data-placeholder")}");
                        Console.WriteLine($"📝 Tag name: {chatGptInput.TagName}");
                        Console.WriteLine($"📝 Text hiện tại: {chatGptInput.Text}");
                        Console.WriteLine($"📝 Có hiển thị không: {chatGptInput.Displayed}");
                        Console.WriteLine($"📝 Có enable không: {chatGptInput.Enabled}");
                    }
                    catch (NoSuchElementException)
                    {
                        Console.WriteLine("❌ Không tìm thấy textarea của ChatGPT!");
                        Console.WriteLine("⚠️ Thử tìm với XPath khác...");
                        try
                        {
                            // Thử đợi một chút và tìm lại
                            Thread.Sleep(2000);
                            Console.WriteLine("🔄 Thử tìm lại sau khi đợi...");
                            chatGptInput = chatGptDriver.FindElement(By.XPath("//p[@data-placeholder='Ask anything']"));
                            Console.WriteLine("✅ Đã tìm thấy sau khi đợi!");
                        }
                        catch (NoSuchElementException)
                        {
                            Console.WriteLine("❌ Vẫn không tìm thấy phần tử nhập văn bản!");
                            Console.WriteLine("⚠️ Hiển thị source HTML để debug:");
                            try
                            {
                                var composerBackground = chatGptDriver.FindElement(By.Id("composer-background"));
                                Console.WriteLine("📝 HTML của composer-background:");
                                Console.WriteLine(composerBackground.GetAttribute("innerHTML"));
                            }
                            catch
                            {
                                Console.WriteLine("❌ Không tìm thấy cả composer-background!");
                            }
                        }
                        Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
                        Console.ReadLine();
                        continue;
                    }
  
                    try
                    {
                        // Đợi và lấy câu trả lời từ ChatGPT
                        var lastResponse = chatGptWait.Until(driver => 
                            driver.FindElement(By.XPath("(//div[contains(@class, \"markdown\")])[last()]")));
  
                        if (lastResponse != null)
                        {
                            // Hiển thị thông tin về phần tử chứa câu trả lời
                            Console.WriteLine("\n🔍 Thông tin về phần tử chứa câu trả lời:");
                            Console.WriteLine($"📝 Class: {lastResponse.GetAttribute("class")}");
                            Console.WriteLine($"📝 Role: {lastResponse.GetAttribute("role")}");
                            
                            Console.WriteLine("\n🤖 ChatGPT trả lời:");
                            Console.WriteLine("------------------------------------------");
                            Console.WriteLine(lastResponse.Text);
                            Console.WriteLine("------------------------------------------\n");
                            Console.WriteLine($"📏 Độ dài câu trả lời: {lastResponse.Text.Length} ký tự");
                            
                            // Lưu nội dung để kiểm tra
                            string copiedText = lastResponse.Text;
                            if (string.IsNullOrEmpty(copiedText))
                            {
                                Console.WriteLine("⚠️ Cảnh báo: Nội dung copy được là rỗng!");
                                
                                // Thử lấy nội dung bằng JavaScript
                                Console.WriteLine("🔄 Thử lấy nội dung bằng JavaScript...");
                                IJavaScriptExecutor js = (IJavaScriptExecutor)chatGptDriver;
                                copiedText = (string)js.ExecuteScript("return arguments[0].textContent;", lastResponse);
                                
                                if (string.IsNullOrEmpty(copiedText))
                                {
                                    Console.WriteLine("❌ Vẫn không lấy được nội dung!");
                                    Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
                                    Console.ReadLine();
                                    continue;
                                }
                                else
                                {
                                    Console.WriteLine("✅ Đã lấy được nội dung bằng JavaScript!");
                                    Console.WriteLine("\n📝 Nội dung lấy được:");
                                    Console.WriteLine("------------------------------------------");
                                    Console.WriteLine(copiedText);
                                    Console.WriteLine("------------------------------------------\n");
                                }
                            }
  
                            // Chuyển sang cửa sổ Kite và gửi tin nhắn
                            Console.WriteLine("🌐 Đang gửi câu trả lời sang Kite...");
                            Console.WriteLine("🔍 Kiểm tra input của Gokite...");
                            try
                            {
                                // Thử tìm input trực tiếp trước
                                IWebElement? kiteInput = null;
                                // Mảng các XPath có thể của input
                                string[] possibleXPaths = new string[] {
                                    "/html/body/div/div[2]/main/div/div[2]/div[3]/form/input",  // XPath ban đầu
                                    "/html/body/div/div[2]/main/div/div[2]/div[2]/form/input"   // XPath sau vòng lặp đầu
                                };

                                Console.WriteLine("🔍 Thử tìm input với các XPath có thể:");
                                try
                                {
                                    foreach (string xpath in possibleXPaths)
                                    {
                                        Console.WriteLine($"🔍 Thử XPath: {xpath}");
                                        try
                                        {
                                            kiteInput = kiteDriver.FindElement(By.XPath(xpath));
                                            Console.WriteLine($"✅ Đã tìm thấy input với XPath: {xpath}");
                                            break;
                                        }
                                        catch (NoSuchElementException)
                                        {
                                            Console.WriteLine($"❌ Không tìm thấy với XPath: {xpath}");
                                            continue;
                                        }
                                    }
                                }
                                catch (NoSuchElementException)
                                {
                                    Console.WriteLine("❌ Không tìm thấy input với tất cả các XPath");
                                    Console.WriteLine("⌛ Đợi thêm 5 giây và thử lại...");
                                    Thread.Sleep(5000);

                                    try
                                    {
                                        // Thử lại với tất cả XPath sau khi đợi
                                        foreach (string xpath in possibleXPaths)
                                        {
                                            Console.WriteLine($"🔍 Thử lại XPath: {xpath}");
                                            try
                                            {
                                                kiteInput = kiteDriver.FindElement(By.XPath(xpath));
                                                Console.WriteLine($"✅ Đã tìm thấy input với XPath: {xpath} sau khi đợi!");
                                                break;
                                            }
                                            catch (NoSuchElementException)
                                            {
                                                Console.WriteLine($"❌ Vẫn không tìm thấy với XPath: {xpath}");
                                                continue;
                                            }
                                        }
                                    }
                                    catch (NoSuchElementException)
                                    {
                                        Console.WriteLine("❌ Vẫn không tìm thấy input của Gokite!");
                                        Console.WriteLine("🔍 Hiển thị HTML hiện tại của Gokite để debug:");
                                        Console.WriteLine(kiteDriver.PageSource.Substring(0, 500) + "...");
                                        Console.WriteLine("\n⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
                                        Console.ReadLine();
                                        return;
                                    }
                                }
                                 
                                if (kiteInput == null)
                                {
                                    throw new Exception("Không tìm thấy input của Gokite sau khi thử nhiều lần");
                                }

                                // Clear ô input và nhập nội dung mới
                                Console.WriteLine("📝 Xóa nội dung cũ và nhập text mới...");
                                kiteInput.Clear();
                                kiteInput.SendKeys(copiedText);
                                
                                // Gửi tin nhắn bằng phím Enter
                                Console.WriteLine("↩️ Gửi Enter...");
                                kiteInput.SendKeys(Keys.Enter);
                                
                                Console.WriteLine("✅ Đã gửi tin nhắn đến Kite!");
                                
                                // Đợi và lấy câu trả lời từ Kite
                                Console.WriteLine("⌛ Đang đợi Kite trả lời...");
                                Console.WriteLine("🔍 Đang tìm phần tử chứa câu trả lời của Kite...");
                                Thread.Sleep(7000); // Đợi 7 giây cho Kite xử lý và hiển thị câu trả lời
  
                                try
                                {
                                    // Lưu xpath để dễ debug
                                    string kiteResponseXPath = "/html/body/div/div[2]/main/div/div[2]/div[1]/div[2]/div/div";
                                    Console.WriteLine($"🔍 Tìm câu trả lời với XPath: {kiteResponseXPath}");
  
                                    // Lấy tất cả các phần tử text trong container
                                    var kiteResponse = kiteDriver.FindElement(By.XPath(kiteResponseXPath));
  
                                    if (kiteResponse != null)
                                    {
                                        Console.WriteLine("✅ Đã tìm thấy phần tử chứa câu trả lời của Kite!");
                                        
                                        // Lấy tất cả text trong container
                                        string responseText = kiteResponse.Text;
                                        
                                        // Hiển thị độ dài của câu trả lời để debug
                                        Console.WriteLine($"📏 Độ dài câu trả lời: {responseText.Length} ký tự");
  
                                        if (string.IsNullOrEmpty(responseText))
                                        {
                                            Console.WriteLine("⚠️ Tìm thấy phần tử nhưng không có nội dung!");
                                            Thread.Sleep(2000); // Đợi thêm 2 giây và thử lại
                                            continue;
                                        }
  
                                        Console.WriteLine("\n🤖 Kite trả lời:");
                                        Console.WriteLine("------------------------------------------");
                                        Console.WriteLine(responseText);
                                        Console.WriteLine("------------------------------------------\n");
  
                                        // Đợi thêm 1 giây sau khi lấy được câu trả lời
                                        Thread.Sleep(1000);
  
                                        // Gửi câu trả lời của Kite sang ChatGPT
                                        Console.WriteLine("📤 Đang gửi câu trả lời sang ChatGPT...");
                                        try 
                                        {
                                            // Tìm lại phần tử nhập văn bản
                                            Console.WriteLine("🔍 Tìm lại phần tử nhập văn bản...");
                                            var inputDiv = chatGptDriver.FindElement(By.XPath("//p[@data-placeholder='Ask anything']"));
                                            
                                            if (inputDiv != null)
                                            {
                                                Console.WriteLine("✅ Đã tìm thấy phần tử nhập văn bản!");
                                                
                                                // Focus và nhập text bằng JavaScript
                                                Console.WriteLine("🔍 Focus vào phần tử...");
                                                IJavaScriptExecutor js = (IJavaScriptExecutor)chatGptDriver;
                                                js.ExecuteScript(@"
                                                    arguments[0].focus();
                                                    arguments[0].innerHTML = arguments[1];
                                                ", inputDiv, responseText);
                                                
                                                Thread.Sleep(500); // Đợi một chút
                                                
                                                // Thử tìm và click nút gửi
                                                Console.WriteLine("🔍 Tìm nút gửi...");
                                                try
                                                {
                                                    var sendButton = chatGptDriver.FindElement(By.CssSelector("button[data-testid='send-button']"));
                                                    if (sendButton != null && sendButton.Enabled)
                                                    {
                                                        Console.WriteLine("🖱️ Click nút gửi...");
                                                        sendButton.Click();
                                                        Console.WriteLine("✅ Đã click nút gửi!");
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine("⚠️ Nút gửi không khả dụng, thử cách khác...");
                                                        // Thử kích hoạt sự kiện Enter bằng JavaScript
                                                        js.ExecuteScript(@"
                                                            const enterEvent = new KeyboardEvent('keydown', {
                                                                bubbles: true,
                                                                cancelable: true,
                                                                key: 'Enter',
                                                                code: 'Enter',
                                                                keyCode: 13,
                                                                which: 13,
                                                                shiftKey: false,
                                                                ctrlKey: false,
                                                                metaKey: false
                                                            });
                                                            arguments[0].dispatchEvent(enterEvent);
                                                        ", inputDiv);
                                                    }
                                                }
                                                catch (NoSuchElementException)
                                                {
                                                    Console.WriteLine("⚠️ Không tìm thấy nút gửi, thử dùng phím Enter...");
                                                    // Thử gửi Enter trực tiếp
                                                    inputDiv.SendKeys(Keys.Enter);
                                                }
                                                
                                                Console.WriteLine("✅ Đã gửi tin nhắn thành công!");
                                            }
                                            else
                                            {
                                                throw new Exception("Không tìm thấy phần tử nhập văn bản");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"❌ Lỗi khi gửi tin nhắn đến ChatGPT: {ex.Message}");
                                            Console.WriteLine("⏸️ Chương trình tạm dừng. Nhấn Enter để thử lại hoặc Ctrl+C để thoát...");
                                            Console.ReadLine();
                                            continue;
                                        }
  
                                        Thread.Sleep(8000); // Đợi ChatGPT xử lý
                                    }
                                    else
                                    {
                                        Console.WriteLine("⚠️ Không tìm thấy nội dung trong phần tử trả lời của Kite");
                                        continue;
                                    }
                                }
                                catch (NoSuchElementException)
                                {
                                    Console.WriteLine("❌ Không tìm thấy phần tử chứa câu trả lời của Kite trên trang");
                                    Console.WriteLine("⚠️ Có thể XPath không chính xác hoặc cấu trúc trang đã thay đổi");
                                    break;
                                }
                                catch (WebDriverTimeoutException)
                                {
                                    Console.WriteLine("❌ Đã hết thời gian chờ khi tìm câu trả lời của Kite");
                                    Console.WriteLine("⚠️ Có thể Kite đang xử lý chậm hoặc không phản hồi");
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"❌ Lỗi không xác định khi xử lý câu trả lời của Kite:");
                                    Console.WriteLine($"⚠️ Chi tiết lỗi: {ex.Message}");
                                    Console.WriteLine($"⚠️ Loại lỗi: {ex.GetType().Name}");
                                    break;
                                }
                            }
                            catch (NoSuchElementException)
                            {
                                Console.WriteLine("❌ Không tìm thấy ô nhập tin nhắn trên Kite");
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠️ Lỗi khi gửi tin nhắn: {ex.Message}");
                                break;
                            }
                        }
                    }
                    catch (NoSuchElementException)
                    {
                        Console.WriteLine("❌ Không tìm thấy câu trả lời nào của ChatGPT");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Lỗi khi tìm câu trả lời: {ex.Message}");
                        break;
                    }
  
                    // Đợi một chút trước khi bắt đầu vòng lặp mới
                    Thread.Sleep(2000);
                }
  
                Console.WriteLine($"\n✨ Đã hoàn thành {conversationCount} lượt hội thoại!");

                // Thêm đoạn code để tự động đóng chương trình
                Console.WriteLine("🔄 Đang chuẩn bị đóng chương trình...");
                isRunning = false;
                CleanupAndExit();
                Environment.Exit(0);
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
        finally
        {
            Console.WriteLine("🔄 Đang dọn dẹp và đóng các trình duyệt...");
            try 
            {
                if (chatGptDriver != null)
                {
                    chatGptDriver.Quit();
                    Console.WriteLine("✅ Đã đóng trình duyệt ChatGPT");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi đóng trình duyệt ChatGPT: {ex.Message}");
            }

            try 
            {
                if (kiteDriver != null)
                {
                    kiteDriver.Quit();
                    Console.WriteLine("✅ Đã đóng trình duyệt Kite");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi đóng trình duyệt Kite: {ex.Message}");
            }

            // Để chắc chắn, kill tất cả các process Chrome còn sót lại
            try 
            {
                foreach (var process in Process.GetProcessesByName("chrome"))
                {
                    process.Kill();
                }
                foreach (var process in Process.GetProcessesByName("chromedriver"))
                {
                    process.Kill();
                }
                Console.WriteLine("✅ Đã dọn dẹp tất cả các process Chrome còn sót");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Lỗi khi kill process Chrome: {ex.Message}");
            }

            Console.WriteLine("👋 Chương trình đã kết thúc");
        }
    }

    static EdgeOptions ConfigureEdgeOptions(bool isKite = true)
    {
        var options = new EdgeOptions();
        try
        {
            if (isKite)
            {
                if (!Directory.Exists(BASE_EDGE_USER_DATA_DIR))
                {
                    Console.WriteLine("[WARN] Edge profile directory not found, creating...");
                    Directory.CreateDirectory(BASE_EDGE_USER_DATA_DIR);
                }
                options.AddArgument($"--user-data-dir={BASE_EDGE_USER_DATA_DIR}");
                options.AddArgument("--profile-directory=Profile 1");
                Console.WriteLine($"[INFO] Using Kite profile: {BASE_EDGE_USER_DATA_DIR}");
            }
            else
            {
                // Đảm bảo đường dẫn ChatGPT profile tồn tại
                if (!Directory.Exists(CHATGPT_USER_DATA_DIR))
                {
                    Console.WriteLine($"[INFO] Creating ChatGPT profile at: {CHATGPT_USER_DATA_DIR}");
                    Directory.CreateDirectory(CHATGPT_USER_DATA_DIR);
                    InitializeChatGPTProfile();
                }
                else
                {
                    Console.WriteLine($"[INFO] Using existing ChatGPT profile: {CHATGPT_USER_DATA_DIR}");
                }
                options.AddArgument($"--user-data-dir={CHATGPT_USER_DATA_DIR}");
                options.AddArgument("--profile-directory=Default");
                Console.WriteLine($"[DEBUG] ChatGPT profile path: {CHATGPT_USER_DATA_DIR}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to configure Edge options: {ex.Message}");
            throw;
        }

        // Các options khác giữ nguyên
        options.AddArgument("--enable-extensions");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--disable-logging");
        options.AddArgument("--log-level=3");
        options.AddArgument("--silent");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        
        return options;
    }

    // Tách riêng phần khởi tạo profile ChatGPT mới
    static void InitializeChatGPTProfile()
    {
        try
        {
            Directory.CreateDirectory(CHATGPT_USER_DATA_DIR);
            var defaultProfilePath = Path.Combine(CHATGPT_USER_DATA_DIR, "Default");
            Directory.CreateDirectory(defaultProfilePath);

            // Copy các file cấu hình từ profile gốc chỉ khi tạo mới
            CopyProfileFiles(Path.Combine(BASE_EDGE_USER_DATA_DIR, "Default"), defaultProfilePath);
            
            Console.WriteLine("[SUCCESS] Created new ChatGPT profile successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to initialize ChatGPT profile: {ex.Message}");
            throw;
        }
    }

    // Thêm phương thức để lấy đường dẫn Edge profile theo từng hệ điều hành
    static string GetEdgeUserDataDir()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string path = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
                Console.WriteLine($"[INFO] Windows Edge profile path: {path}");
                return path;
            }
            else if (OperatingSystem.IsMacOS())
            {
                string username = Environment.UserName;
                string path = Path.Combine(
                    "/Users", username,
                    "Library", "Application Support", "Microsoft Edge", "User Data"
                );
                Console.WriteLine($"[INFO] MacOS Edge profile path: {path}");
                return path;
            }
            else if (OperatingSystem.IsLinux())
            {
                string username = Environment.UserName;
                string path = Path.Combine(
                    "/home", username,
                    ".config", "microsoft-edge", "User Data"
                );
                Console.WriteLine($"[INFO] Linux Edge profile path: {path}");
                return path;
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to get Edge profile path: {ex.Message}");
            throw;
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

    static IWebDriver? StartEdgeWithProfile(string profileName, string userDataDir)
    {
        try
        {
            KillAllEdgeProcesses();
            Thread.Sleep(2000);

            Console.WriteLine($"📁 Sử dụng profile {profileName} từ {userDataDir}");

            var options = new EdgeOptions();
            options.AddArgument($"--user-data-dir={userDataDir}");
            options.AddArgument($"--profile-directory={profileName}");
            
            // In ra đường dẫn thực tế được sử dụng
            Console.WriteLine($"🔍 Sử dụng user data dir: {userDataDir}");
            Console.WriteLine($"🔍 Sử dụng profile: {profileName}");

            options.AddArgument("--start-maximized");
            options.AddArgument("--no-first-run");
            options.AddArgument("--no-default-browser-check");
            options.AddArgument("--password-store=basic");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            
            var driver = new EdgeDriver(options);

            return driver;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Lỗi khi mở {profileName}: {ex.Message}");
            return null;
        }
    }

    static void InitializeEdgeProfiles()
    {
        try
        {
            Console.WriteLine("[INFO] Checking Edge profiles...");

            // Kiểm tra và tạo thư mục profile cho Kite
            if (!Directory.Exists(BASE_EDGE_USER_DATA_DIR))
            {
                Console.WriteLine("[INFO] Creating base Edge profile directory...");
                Directory.CreateDirectory(BASE_EDGE_USER_DATA_DIR);
                
                // Copy các file cấu hình cơ bản từ profile mặc định nếu có
                string defaultProfilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Edge", "User Data"
                );

                if (Directory.Exists(defaultProfilePath))
                {
                    Console.WriteLine("[INFO] Copying default profile settings...");
                    CopyProfileFiles(defaultProfilePath, BASE_EDGE_USER_DATA_DIR);
                }
            }

            // Kiểm tra và tạo Profile 1 cho Kite
            string kiteProfilePath = Path.Combine(BASE_EDGE_USER_DATA_DIR, "Profile 1");
            if (!Directory.Exists(kiteProfilePath))
            {
                Console.WriteLine("[INFO] Creating Kite profile directory...");
                Directory.CreateDirectory(kiteProfilePath);
                
                // Tạo file Preferences cơ bản cho Profile 1
                CreateDefaultPreferences(kiteProfilePath, "Kite Profile");
            }

            // Kiểm tra và tạo profile cho ChatGPT
            if (!Directory.Exists(CHATGPT_USER_DATA_DIR))
            {
                Console.WriteLine("[INFO] Creating ChatGPT profile directory...");
                Directory.CreateDirectory(CHATGPT_USER_DATA_DIR);
                
                // Tạo file Preferences cơ bản cho ChatGPT
                CreateDefaultPreferences(Path.Combine(CHATGPT_USER_DATA_DIR, "Default"), "ChatGPT Profile");
            }

            Console.WriteLine("[SUCCESS] Edge profiles initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to initialize profiles: {ex.Message}");
            throw;
        }
    }

    static void CopyProfileFiles(string sourcePath, string targetPath)
    {
        try
        {
            // Danh sách các file cấu hình cần copy
            string[] configFiles = {
                "Local State",
                "Preferences",
                "Secure Preferences"
            };

            foreach (string file in configFiles)
            {
                string sourceFile = Path.Combine(sourcePath, file);
                string targetFile = Path.Combine(targetPath, file);

                if (File.Exists(sourceFile) && !File.Exists(targetFile))
                {
                    File.Copy(sourceFile, targetFile);
                    Console.WriteLine($"[INFO] Copied {file}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Error copying profile files: {ex.Message}");
        }
    }

    static void CreateDefaultPreferences(string profilePath, string profileName)
    {
        try
        {
            Directory.CreateDirectory(profilePath);

            // Tạo file Preferences với cấu hình cơ bản
            var preferences = new
            {
                profile = new
                {
                    name = profileName,
                    exit_type = "Normal",
                    exited_cleanly = true
                },
                browser = new
                {
                    enabled_labs_experiments = new string[] { },
                    has_seen_welcome_page = true
                },
                extensions = new
                {
                    settings = new { }
                }
            };

            string preferencesPath = Path.Combine(profilePath, "Preferences");
            File.WriteAllText(preferencesPath, JsonSerializer.Serialize(preferences));
            Console.WriteLine($"[INFO] Created Preferences for {profileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Error creating preferences: {ex.Message}");
        }
    }
}
