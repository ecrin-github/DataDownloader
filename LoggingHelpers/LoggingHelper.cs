
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace DataDownloader
{
    public class LoggingHelper
    {
        private string logfile_startofpath;
        private string logfile_path;
        private string summary_logfile_startofpath;
        private string summary_logfile_path;
        private string summary_text;
        private StreamWriter sw;

        public LoggingHelper()
        {
            IConfigurationRoot settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            logfile_startofpath = settings["logfilepath"];
            summary_logfile_startofpath = settings["summaryfilepath"];
        }

        // Used to check if a log file with a named source has been created
        public string LogFilePath => logfile_path;


        public void OpenLogFile(Source source, string source_file_name)
        {

            string dt_string = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                              .Replace(":", "").Replace("T", " ");
            string log_folder_path = Path.Combine(logfile_startofpath, source.database_name);  

            if (!Directory.Exists(log_folder_path))
            {
                Directory.CreateDirectory(log_folder_path);
            }

            logfile_path = Path.Combine(log_folder_path, "DL " + source.database_name + " " + dt_string);
            summary_logfile_path = Path.Combine(summary_logfile_startofpath, "DL " + source.database_name + " " + dt_string);

            // source file name used for WHO case, where the source is a file
            // In other cases is not required

            if (source_file_name != null)
            {
                string file_name = source_file_name.Substring(source_file_name.LastIndexOf("\\") + 1);
                logfile_path += " USING " + file_name + ".log";
            }
            else
            {
                logfile_path += ".log";
            }
            
            sw = new StreamWriter(logfile_path, true, System.Text.Encoding.UTF8);
        }


        public void OpenNoSourceLogFile()
        {
            string dt_string = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                              .Replace("-", "").Replace(":", "").Replace("T", " ");
            logfile_path += logfile_startofpath + "DL No Source " + dt_string + ".log";
            sw = new StreamWriter(logfile_path, true, System.Text.Encoding.UTF8);
        }


        public void LogLine(string message, string identifier = "")
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string feedback = dt_string + message + identifier;
            Transmit(feedback);
         }


        public void LogHeader(string message)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string header = dt_string + "**** " + message + " ****";
           Transmit("");
           Transmit(header);
        }


        public void LogError(string message)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string error_message = dt_string + "***ERROR*** " + message;
            Transmit("");
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit(error_message);
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit("");

            summary_text += "\nTRAPPED ERROR: " + message;
        }


        public void LogCodeError(string header, string errorMessage, string stackTrace)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            string headerMessage = dt_string + "***ERROR*** " + header + "\n";
            Transmit("");
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit(headerMessage);
            Transmit(errorMessage + "\n");
            Transmit(stackTrace);
            Transmit("+++++++++++++++++++++++++++++++++++++++");
            Transmit("");

            summary_text += "\nUNTRAPPED ERROR: " ;
            summary_text += "\nERROR MESSAGE: ";
            summary_text += "\nSTACK TRACE: ";
        }


        public void LogRes(DownloadResult res)
        {
            string dt_string = DateTime.Now.ToShortDateString() + " : " + DateTime.Now.ToShortTimeString() + " :   ";
            TransmitAndAddToSummary("");
            TransmitAndAddToSummary(dt_string + "**** " + "Download Result" + " ****");
            TransmitAndAddToSummary(dt_string + "**** " + "Records checked: " + res.num_checked.ToString() + " ****");
            TransmitAndAddToSummary(dt_string + "**** " + "Records downloaded: " + res.num_downloaded.ToString() + " ****");
            TransmitAndAddToSummary(dt_string + "**** " + "Records added: " + res.num_added.ToString() + " ****");
        }


        public void CloseLog()
        {
            LogHeader("Closing Log");
            sw.Flush();
            sw.Close();

            if (!string.IsNullOrEmpty(summary_text))
            {
                sw = new StreamWriter(summary_logfile_path, true, System.Text.Encoding.UTF8);
                sw.WriteLineAsync(summary_text);
                sw.Flush();
                sw.Close();

                // transfer file to mail pickup folder

            }
        }


        private void Transmit(string message)
        {
            sw.WriteLine(message);
            Console.WriteLine(message);
        }


        private void TransmitAndAddToSummary(string message)
        {
            sw.WriteLine(message);
            Console.WriteLine(message);
            summary_text += "\n" + message;
        }


        public void SendEMail(string message)
        {
            sw = new StreamWriter(summary_logfile_path, true, System.Text.Encoding.UTF8);


            sw.WriteLine(summary_text);


            sw.Flush();
            sw.Close();

            // transfer file to mail pickup folder

        }



        public string LogArgsParameters(Args args)
        {

            LogLine("****** DOWNLOAD ******");
            LogHeader("Set up");
            LogLine("source_id is " + args.source_id.ToString());
            LogLine("type_id is " + args.type_id.ToString());
            string file_name = (args.file_name == null) ? " was not provided" : " is " + args.file_name;
            LogLine("file_name" + file_name);
            string cutoff_date = (args.cutoff_date == null) ? " was not provided" : " is " + ((DateTime)args.cutoff_date).ToShortDateString();
            LogLine("cutoff_date" + cutoff_date);
            string filter_id = (args.filter_id == null) ? " was not provided" : " is " + args.filter_id.ToString();
            LogLine("filter" + filter_id);
            string ignore_recent_days = (args.skip_recent_days == null) ? " was not provided" : " is " + args.skip_recent_days.ToString();
            LogLine("ignore recent downloads parameter" + ignore_recent_days);
            string start_page = (args.start_page == null) ? " was not provided" : " is " + args.start_page.ToString();
            LogLine("start page" + start_page);
            string end_page = (args.end_page == null) ? " was not provided" : " is " + args.end_page.ToString();
            LogLine("end page" + end_page);

            string previous_saf_ids = "";
            if (args.previous_searches.Count() > 0)
            {
                foreach (int i in args.previous_searches)
                {
                    LogLine("previous_search is " + i.ToString());
                    previous_saf_ids += ", " + i.ToString();
                }
                previous_saf_ids = previous_saf_ids.Substring(2);
            }
            string no_logging = (args.filter_id == null) ? " was not provided" : " is " + args.no_logging;
            LogLine("no_Logging" + no_logging);
            return previous_saf_ids;
        }


        public void SendEmail(string error_message_text)
        {
            // construct txt file with message
            // and place in pickup folder for
            // SMTP service (if possible - may need to change permissions on folder)


        }


        public void SendRes(string result_text)
        {
            // construct txt file with message
            // and place in pickup folder for
            // SMTP service (if possible - may need to change permissions on folder)


        }

    }
}

