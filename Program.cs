﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using TrifleJS.Properties;

namespace TrifleJS
{
    class Program
    {
        public static API.Context Context { get; set; }
        public static string[] Args { get; set; }
        public static bool Verbose { get; set; }
        public static bool InEventLoop { get; set; }

        /// <summary>
        /// Help message
        /// </summary>
        static void Help()
        {
            Console.WriteLine();
            Console.WriteLine("==========================");
            Console.WriteLine("TrifleJS.exe");
            Console.WriteLine("Headless automation for Internet Explorer");
            Console.WriteLine("==========================");
            Console.WriteLine("http://triflejs.org/");
            Console.WriteLine();
            Console.WriteLine("A headless Internet Explorer emulator with JavaScript API running on V8 engine.");
            Console.WriteLine("PhantomJS for the Trident Engine.");
            Console.WriteLine();
            Console.WriteLine("(c) Steven de Salas 2013 - MIT Licence");
            Console.WriteLine();
            Console.WriteLine("Usage: triflejs.exe [options] somescript.js [argument [argument [...]]]");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            Console.WriteLine("  --debug                           Prints additional warning and debug messages.");
            //Console.WriteLine("  --ignore-ssl-errors=[true|false]  Ignores SSL errors (cert expired, invalid etc). Defaults to 'false'.");
            Console.WriteLine("  --proxy=[address:port]            Specifies proxy server to use.");
            Console.WriteLine("  --proxy-auth=[user:passw]         Authentication information for the proxy.");
            Console.WriteLine("  --render=[url]                    Opens a url, renders into a file and quits.");
            Console.WriteLine("  --emulate=[version]               Emulates an earlier version of IE (IE7, IE8, IE9 etc).");
            Console.WriteLine();
            Console.WriteLine("  -h, --help                  Show this message and quits");
            Console.WriteLine("  -t, --test                  Runs a System Test and quits");
            Console.WriteLine("  -v, --version               Prints out TrifleJS version and quits");
            Console.WriteLine();
            Console.WriteLine("Without arguments, TrifleJS will launch in interactive mode (REPL)");
            Console.WriteLine();
        }

        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        static void Main(string[] args)
        {
            // Commence
            Program.Args = args;
#if DEBUG
            Program.Verbose = true;
            Utils.Debug("{0} {1}", AppDomain.CurrentDomain.FriendlyName, String.Join(" ", args));
#endif
            // Define environment
            bool isExecuted = false;
            bool isVersionSet = false;
            List<string> configLoop = new List<string>(args);
            List<string> commandLoop = new List<string>();
            API.Phantom.outputEncoding = "UTF-8";
            API.Phantom.cookiesEnabled = true;
            API.Phantom.libraryPath = Environment.CurrentDirectory;

            // Check OS Support
            CheckSupport();

            // No arguments? Run in interactive mode.
            if (args.Length < 1) {
                Interactive();
                return;
            }

            // Config Loop (Set IE version etc)
            foreach (string arg in configLoop)
            {
                string[] parts = arg.Split('=');
                switch (parts[0]) 
                {
                    case "-?":
                    case "/?":
                    case "-h":
                    case "--help":
                        Help();
                        return;
                    case "--debug":
                        Program.Verbose = true;
                        break;
                    case "-v":
                    case "--version":
                        var v = API.Phantom.version;
                        Console.WriteLine("{0}.{1}.{2}", v["major"], v["minor"], v["patch"]);
                        return;
                    case "--emulate":
                        isVersionSet = Browser.Emulate(arg.Replace("--emulate=", "").ToUpper()); 
                        break;
                    case "--proxy":
                        Proxy.server = arg.Replace("--proxy=", "");
                        break;
                    case "--proxy-auth":
                        Proxy.auth = arg.Replace("--proxy-auth=", "");
                        break;
                    case "--proxy-type":
                        Proxy.type = arg.Replace("--proxy-type=", "");
                        break;
                    case "--ignore-ssl-errors":
                        if (arg.Replace("--ignore-ssl-errors=", "").ToLower().Equals("true")) {
                            // TODO
                        }
                        break;
                    default:
                        commandLoop.Add(arg);
                        break;
                }
            }

            // Default to Installed Version
            if (!isVersionSet)
            {
                String version = Browser.InstalledVersion();
                if (!String.IsNullOrEmpty(version))
                    Browser.Emulate(version);
                else
                    // No version? use IE7
                    Browser.Emulate("IE7");
            }

            // Set proxy information if needed
            if (!String.IsNullOrEmpty(Proxy.server)) {
                Proxy.Set();
            }

            // Command Loop - Execution
            foreach (string arg in commandLoop)
            {
                string[] parts = arg.Split('=');
                switch (parts[0]) 
                { 
                    case "-t":
                    case "--test":
                        UnitTest();
                        Exit(0);
                        return;
                    case "--render":
                        string url = arg.Replace("--render=", "");
                        Render(url);
                        return;
                    default:
                        // If no switch is defined then we are dealing 
                        // with javascript files that need executing
                        if (arg == parts[0] && !isExecuted)
                        {
                            Open(arg);
                            isExecuted = true;
                        }
                        else if (parts[0].StartsWith("--")) {
                            Help();
                            Exit(0);
                        }
                        break;
                }
            }

            Exit(0);

        }

        /// <summary>
        /// Checks support for various OS features
        /// </summary>
        private static void CheckSupport()
        {
            if (!System.Net.HttpListener.IsSupported)
            {
                API.Console.error("Windows XP SP2 or Server 2003 is required.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Exits the current thread
        /// </summary>
        /// <param name="exitCode"></param>
        public static void Exit(int exitCode)
        {
            Proxy.Backup.Restore();
#if DEBUG
            // Debugging? Wait for input
            Console.WriteLine();
            Console.WriteLine("Press any key to finish...");
            Console.Read();
#endif
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Runs compatibility tests
        /// </summary>
        static void PhantomTest() {

            Console.WriteLine();
            Console.WriteLine("============================================");
            Console.WriteLine("TrifleJS -- Phantom Compatibility Test");
            Console.WriteLine("============================================");
            Console.WriteLine();

            using (Program.Context = Initialise())
            {
                try
                {
                    // Load libs
                    Context.RunScript(Resources.test_lib_jasmine, "test/lib/jasmine.js");
                    Context.RunScript(Resources.test_lib_jasmine_console, "test/lib/jasmine-console.js");
                    Context.RunScript(Resources.test_phantom_tools, "test/phantom/tools.js");

                    // Load Spec
                    Context.RunScript(Resources.test_phantom_spec_phantom, "test/phantom/phantom.js");
                    Context.RunScript(Resources.test_phantom_spec_webserver, "test/phantom/webserver.js");
                    Context.RunScript(Resources.test_phantom_spec_webserver, "test/phantom/webpage.js");

                    // Execute
                    Context.RunScript(Resources.test_run_jasmine, "test/phantom/run-jasmine.js");

                    // Keep running until told to stop
                    // This is to make sure asynchronous code gets executed
                    while (true)
                    {
                        Program.DoEvents();
                    }

                }
                catch (Exception ex)
                {
                    // Handle any exceptions
                    API.Context.Handle(ex);
                }
            }
        }

        /// <summary>
        /// Runs unit tests
        /// </summary>
        static void UnitTest()
        {

            Console.WriteLine();
            Console.WriteLine("============================================");
            Console.WriteLine("TrifleJS -- Unit Tests");
            Console.WriteLine("============================================");

            Program.Verbose = false; 

            using (Program.Context = Initialise())
            {
                try
                {
                    // Load libs
                    Context.RunScript(Resources.test_unit_tools, "test/unit/tools.js");

                    // Execute Specs
                    Context.RunScript(Resources.test_unit_spec_require, "test/unit/spec/require.js");
                    Context.RunScript(Resources.test_unit_spec_fs, "test/unit/spec/fs.js");
                    Context.RunScript(Resources.test_unit_spec_phantom, "test/unit/spec/phantom.js");
                    Context.RunScript(Resources.test_unit_spec_webserver, "test/unit/spec/webserver.js");
                    Context.RunScript(Resources.test_unit_spec_webpage, "test/unit/spec/webpage.js");

                    // Finish
                    Context.RunScript(Resources.test_unit_finish, "test/unit/finish.js");

                    // Keep running until told to stop
                    // This is to make sure asynchronous code gets executed
                    while (true)
                    {
                        Program.DoEvents();
                    }

                }
                catch (Exception ex)
                {
                    // Handle any exceptions
                    API.Context.Handle(ex);
                }
            }
        }


        /// <summary>
        /// Run TrifleJS in interactive mode
        /// </summary>
        static void Interactive()
        {
            // Initialize and start console read loop;
            using (Program.Context = Initialise()) 
            {
                while (true)
                {
                    Console.Write("triflejs> ");
                    try
                    {
                        API.Console.log(Context.Run(Console.ReadLine(), "REPL"));
                    }
                    catch (Exception ex)
                    {
                        API.Context.Handle(ex);
                    }
                }
            }   
        }

        /// <summary>
        /// Renders a url
        /// </summary>
        /// <param name="url"></param>
        static void Render(string url) 
        {
            Console.WriteLine("Rendering " + url + "...");

            // Check the URL
            Uri uri = Browser.TryParse(url);
            if (uri == null) {
                Console.Error.WriteLine("Unable to open url: " + url);
                return;
            }

            // Continue if ok
            using (var browser = new Browser())
            {
                browser.Size = new Size(1024, 700);
                browser.Navigate(url);
                browser.RenderOnLoad(String.IsNullOrEmpty(uri.Host) ? "page.png" : uri.Host + ".png");

                while (browser.ReadyState != System.Windows.Forms.WebBrowserReadyState.Complete)
                {
                    Program.DoEvents();
                }
            }
        }

        /// <summary>
        /// Opens a javascript file and executes in host environment
        /// </summary>
        /// <param name="filename">Path to a javascript file</param>
        static void Open(string filename) 
        {
            // Check file
            if (!File.Exists(filename))
            {
                Console.Error.WriteLine(String.Format("File does not exist: {0}", filename));
                return;
            }

            //Initialize a context
            using (Program.Context = Initialise())
            {
                // Set Library Path
                API.Phantom.libraryPath = new FileInfo(filename).DirectoryName;
                API.Phantom.scriptName = new FileInfo(filename).Name;

                try
                {
                    // Run the script
                    Context.RunFile(filename);

                    // Keep running until told to stop
                    // This is to make sure asynchronous code gets executed
                    while (true) {
                        Program.DoEvents();
                    }

                }
                catch (Exception ex) {
                    // Handle any exceptions
                    API.Context.Handle(ex);
                }
            }
        }

        /// <summary>
        /// Runs background events while waiting, 
        /// make sure that we track when we are on the 
        /// event loop to avoid recursion.
        /// </summary>
        public static void DoEvents()
        {
            Program.InEventLoop = true;
            GC.Collect();
            API.Window.CheckTimers();
            API.Modules.WebServer.ProcessConnections();
            System.Windows.Forms.Application.DoEvents();
            Program.InEventLoop = false;
        }

        /// <summary>
        /// Initialises the environment
        /// </summary>
        /// <returns></returns>
        static API.Context Initialise()
        {
            // Create new context
            API.Context context = new API.Context();

            // Setting core global variables
            context.SetParameter("console", new API.Console());
            context.SetParameter("phantom", new API.Phantom());
            context.SetParameter("trifle", new API.Trifle());
            context.SetParameter("window", new API.Window());

            try
            {
                // Initialise host env
                context.RunScript(Resources.init, "init.js");
                context.RunScript(Resources.trifle_Callback, "trifle.Callback.js");
                context.RunScript(Resources.trifle_modules_WebPage, "trifle.modules.WebPage.js");
                context.RunScript(Resources.trifle_modules_FileSystem, "trifle.modules.FileSystem.js");
                context.RunScript(Resources.trifle_modules_System, "trifle.modules.System.js");
                context.RunScript(Resources.trifle_modules_WebServer, "trifle.modules.WebServer.js");
            }
            catch (Exception ex)
            {
                API.Context.Handle(ex);
            }

            // Return context
            return context;
        }

    }
}
