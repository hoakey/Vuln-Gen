using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace execute
{
    class Program
    {
        static void Main(string[] args)
        {
         // Manage user arguments with default arguments
         Dictionary<string, string> options = SetArgs(args);

         try
         {
            ValidateRun(options);
         }
         catch (Exception e)
         {
            Console.WriteLine("ERROR: Some files missing");
            Console.WriteLine("Message: " + e.Message);
            Console.Write("Press any key to exit...");
            Console.ReadKey();
            return;
         }

         WriteFiles(options);

         // Launch packer
         StartPacker(options);
        }

      static void StartPacker(Dictionary<string, string> options)
      {
         string pwd = options["working_directory"];

         // Fetch arguments not going to packer
         string templatePath = pwd + options["template_outpath"];
         options.Remove("template_outpath");

         string packerPath = pwd + options["packerpath"];
         options.Remove("packerPath");

         var packerCall = new System.Diagnostics.Process();

         // Add all call arguments to arguments string
         string argsString = "build ";
         /*foreach (var arg in options)
         {
            argsString += $"-var {arg.Key}={arg.Value} ";
         }*/
         argsString += templatePath;

         // Declare Process Info with packer exe and arguments
         var callInfo = new ProcessStartInfo(packerPath, argsString)
         {
            // Extra options
            WindowStyle = ProcessWindowStyle.Minimized,
            WorkingDirectory = pwd
         };

         // Apply the call info to the process
         packerCall.StartInfo = callInfo;

         // Set function call on completion
         packerCall.EnableRaisingEvents = true;
         packerCall.Exited += new EventHandler(Packer_Exited);

         // Call packer
         packerCall.Start();
         packerCall.WaitForExit();
      }

      /// <summary>
      /// Called when packer process has finished
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      static void Packer_Exited(object sender, EventArgs e)
      {
         // Tell the user we're done
         Console.WriteLine("\nPacker has completed and exited. See above for details.\nPress any key to exit...");
         Console.ReadKey();
      }

      /// <summary>
      /// Parses arguments passed in from the user. Sets default values not set by user.
      /// </summary>
      /// <param name="args">The command line arguments string array</param>
      /// <returns>Dictionary of all possible options and current values</returns>
      static Dictionary<string, string> SetArgs(string[] args)
      {
         var parsedArgs = new Dictionary<string, string>();
         string install_instructions = "";

         // Look for strings like name=value
         Regex cmdRegEx = new Regex(@"(?<name>.+?)=(?<val>.+)");

         // Sort the keys and values into a dictionary for manipulating
         foreach (string s in args)
         {
            Match m = cmdRegEx.Match(s);
            if (m.Success)
            {
               // Handle special-case argument
               if (m.Groups[1].Value.ToLower() == "attack")
                  install_instructions = addInstruction(m.Groups[2].Value.ToUpper(), install_instructions);
               else
                  parsedArgs.Add(m.Groups[1].Value, m.Groups[2].Value.ToLower());
            }
         }

         

         // Set the default values for arguments not defined by the user
         // TODO: Make these read in from config file and then override the values
         Dictionary<string, string> defaultSettings = new Dictionary<string, string>
         {
            { "ssh_name", "default" },
            { "ssh_pass", "default" },
            { "hostname", "vuln-server" },
            { "packerpath", @"\bin\packer.exe" },
            { "templatepath", @"\templates\vboxtemplate.json" },
            { "working_directory", @"C:\packer_data" },
            { "preseed_outpath",  @"\http_directory\preseed.cfg"},
            { "preseed_inpath",   @"\http_directory\genericpreseed.cfg" },
            { "template_outpath",  @"\templates\vboxtemplate.json"},
            { "template_inpath",  @"\templates\genericvboxtemplate.json"},
            { "iso_path",  @"\isos\ubuntu-12.04.5-server-amd64.iso"},
            { "install_instructions", "" }
         };

         // Add the attack instructions to our values
         parsedArgs.Add("install_instructions", install_instructions);

         // Add default values to arguments dictionary
         foreach (var defaultVal in defaultSettings)
         {
            // User has not defined value for this setting
            if (!parsedArgs.ContainsKey(defaultVal.Key))
            {
               parsedArgs.Add(defaultVal.Key, defaultVal.Value);

               // Inform the user what the default value is
               Console.WriteLine($"{defaultVal.Key} not user defined. {defaultVal.Key} will be {defaultVal.Value}");
            }
         }
         Console.WriteLine();

         // ISO path needs to be fully qualified and produce two slashes per directory in the output file
         parsedArgs["iso_path"] = parsedArgs["working_directory"] + parsedArgs["iso_path"].Replace(@"\", @"\\");

         return parsedArgs;
      }


      /// <summary>
      /// Validates that all files noted in options are available. Throws exceptions if not.
      /// </summary>
      /// <param name="options">The user defined options for this run</param>
      /// <returns>True if everything validates. Exception with error string if anything fails</returns>
      static bool ValidateRun(Dictionary<string, string> options)
      {
         // Product run from pwd?
         if (Directory.Exists("\\bin") && Directory.Exists("\\http_directory"))
            // Set working_directory option to be the pwd
            options["working_directory"] = Directory.GetCurrentDirectory();
         // Did user set working_directory flag instead?
         else if (!Directory.Exists(options["working_directory"] + "\bin") && !Directory.Exists(options["working_directory"] + "\\http_directory"))
            throw new Exception("Run from wrong directory. Please run me from my location or set 'working_directory' to executable directory.");

         string pwd = options["working_directory"];

         // Packer is installated in expected location?
         if (!File.Exists(pwd + options["packerpath"]))
            throw new Exception("Could not locate packer.exe. Please set 'packerpath' to executable location.");

         // Preseed available?
         if (!File.Exists(pwd + options["preseed_inpath"]))
            throw new Exception("Could not locate a generic preseed.cfg file. Set 'preseed_inpath' to file location.");

         // Preseed in and out are the different?
         if (options["preseed_inpath"] == options["preseed_outpath"])
            throw new Exception("Output preseed.cfg file would overwrite generic preseed input template. Set 'preseed_outpath' to set output file location.");

         // Template available?
         if (!File.Exists(pwd + options["template_inpath"]))
            throw new Exception("Could not locate a generic preseed.cfg file. Set 'preseed_inpath' to file location.");

         // Template in and out are the different?
         if (options["template_inpath"] == options["template_outpath"])
            throw new Exception("Output template file would overwrite generic template input template. Set 'template_outpath' to set output file location.");

         return true;
      }

      /// <summary>
      /// Generates a runtime for file for this execution from existing boilerplate files
      /// </summary>
      /// <param name="options">The dictionary of global options for this execution</param>
      static void WriteFiles(Dictionary<string, string> options)
      {
         int counter = 0;
         string line;
         string pwd = options["working_directory"];

         // It's really just a key/value array
         // Key is input, value is output
         var files = new Dictionary<string, string>
         {
            // Preseed file
            { pwd + options["preseed_inpath"],
               pwd + options["preseed_outpath"]},
            // Template
            { pwd + options["template_inpath"],
               pwd + options["template_outpath"]}
         };

         foreach (var key in files.Keys)
         {
            // options[key] == value
            // Delete the output file if one already exists
            if (File.Exists(files[key]))
               File.Delete(files[key]);

            using (StreamWriter sw = File.CreateText(files[key]))
            {
               // Read the file and display it line by line.  
               System.IO.StreamReader file =
                   new System.IO.StreamReader(key);
               while ((line = file.ReadLine()) != null)
               {
                  counter++;

                  // Search the line for a { delimeter
                  for (int i = 0; i < line.Length; i++)
                  {
                     // If a delimeter is found add each char to an array
                     // until the next one is found. If the variable is
                     // recognized replace it
                     if (line[i] == '{')
                     {
                        // Get whole token
                        string token = "";
                        do
                        {
                           token += line[i];
                        } while (line[i++] != '}' && i < line.Length);

                        if (token.Length > 2)
                        {
                           // Get only the meaningful contents of the token
                           var variable = token.Substring(1, token.Length - 2);
                           if (options.ContainsKey(variable))
                           {
                              line = line.Replace(token, options[variable]);
                              // Rescan the line and avoid out of range exceptions
                              i = 0;
                           }
                           // If we can't find the variable assume the user needs it for whatever
                           else
                              Console.WriteLine($"Found token {token} but I don't know what that is. Leaving it as is on line {counter}.");
                        }
                     }
                  }
                  // Write the line to our working file whether or not a variable has been found to replace
                  sw.WriteLine(line);
               }
               // VERY IMPORTANT LINE HERE
               file.Close();
            }
         }
      }


      /// <summary>
      /// Adds an install instruction to install the desired piece of vulnerable software.
      /// </summary>
      /// <param name="attackName">A known name to call the attack by</param>
      /// <param name="instruct">Any old installation instructions in case this method is
      /// called more than once.</param>
      /// <returns>Commands for a Packer template file that Packer will recognize</returns>
      static string addInstruction(string attackName, string instruct = "")
      {
         // Add a comma and line break to the end of the line if needed
         if (instruct.Length != 0 && instruct[instruct.Length - 1] != ',') 
         {
            instruct += ',';
            instruct += Environment.NewLine;
         }

         switch (attackName)
         {
            //http://www.securityfocus.com/bid/34562
            case "CVE-2008-5518":
            case "CVE-2009-0038":
            case "CVE-2009-0039":
               instruct += "\"sudo apt-get install apache=2.1.3\"";
               break;

            //https://www.cvedetails.com/cve/CVE-1999-0067/
            case "CVE-1999-0067":
               instruct += "\"sudo apt-get install apache=1.0.3\"";
               break;

            //https://www.cvedetails.com/cve/CVE-2014-3524/
            case "CVE-2014-3524":
               instruct += "\"sudo add-apt-repository ppa:upubuntu-com/openoffice\"" + Environment.NewLine + ',';
               instruct += "\"sudo apt-get update\"" + Environment.NewLine + ',';
               instruct += "\"sudo apt-get install apache-openoffice\"";
               break;

            case "CVE-2017-9788":
               instruct += "\"sudo apt-get install -y apache2=2.4.26\"";
               break;

            default:
               break;
         }
         return instruct;
      }
   }
}
