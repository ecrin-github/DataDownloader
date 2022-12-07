using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace DataDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // check and process args
            var parsedArguments = Parser.Default.ParseArguments<Options>(args);
            await parsedArguments.WithParsedAsync(opts => RunOptionsAndReturnExitCodeAsync(opts));
            await parsedArguments.WithNotParsedAsync((errs) => HandleParseErrorAsync(errs));
        }

        static async Task<int> RunOptionsAndReturnExitCodeAsync(Options opts)
        {
            // Handle options.
            Args args = new Args();
            LoggingHelper _logging_helper = new LoggingHelper();
            MonitorDataLayer _monitor_repo = new MonitorDataLayer(_logging_helper);

            // Check source id is valid. If it is, the source property of the 
            // monitor_repo is also set as part of the function.
            try
            {
                Source source = _monitor_repo.FetchSourceParameters(opts.source_id);
                if (source == null)
                {
                    // N.B. No log file available yet - needs to be created in exception handler
                    throw new ArgumentException("The first argument does not correspond to a known source");
                }
                args.source_id = source.id;
                _logging_helper.OpenLogFile(source, opts.file_name);

                // Check source fetch type id is valid. 

                SFType sf_type = _monitor_repo.FetchTypeParameters(opts.search_fetch_type_id);
                if (sf_type == null)
                {
                    throw new ArgumentException("the type argument does not correspond to a known search / fetch type");
                }
                args.type_id = sf_type.id;


                // do the filter type if any before the date
                // as may be required in the fetch last date function

                args.filter_id = opts.focused_search_id;
                if (sf_type.requires_search_id)
                {
                    if (args.filter_id == 0 || args.filter_id == null)
                    {
                        string error_message = "This search fetch type requires an integer referencing a search type";
                        error_message += " and no valid filter (search) id is supplied";
                        throw new ArgumentException(error_message);
                    }
                }


                // If a date is required check one is present and is valid. 
                // It should be in the ISO YYYY-MM-DD format.

                if (sf_type.requires_date)
                {
                    string cutoff_date = opts.cutoff_date;
                    if (!string.IsNullOrEmpty(cutoff_date))
                    {
                        if (Regex.Match(cutoff_date, @"^20\d{2}-[0,1]\d{1}-[0, 1, 2, 3]\d{1}$").Success)
                        {
                            args.cutoff_date = new DateTime(
                                        Int32.Parse(cutoff_date.Substring(0, 4)),
                                        Int32.Parse(cutoff_date.Substring(5, 2)),
                                        Int32.Parse(cutoff_date.Substring(8, 2)));
                        }
                    }
                    else
                    {
                        // Try and find the last download date and use that
                        if (sf_type.requires_search_id)
                        {
                            args.cutoff_date = _monitor_repo.ObtainLastDownloadDateWithFilter(source.id, (int)args.filter_id);
                        }
                        else
                        {
                            args.cutoff_date = _monitor_repo.ObtainLastDownloadDate(source.id);
                        }
                    }

                    if (args.cutoff_date == null)
                    {
                        string error_message = "This search fetch type requires a date";
                        error_message += " in the format YYYY-MM-DD and this is missing";
                        throw new ArgumentException(error_message);
                    }
                }

                // If a file is required check a name is supplied and that it
                // corresponds to a file.

                args.file_name = opts.file_name;
                if (sf_type.requires_file)
                {
                    if (string.IsNullOrEmpty(args.file_name) || !File.Exists(args.file_name))
                    {
                        string error_message = "This search fetch type requires a file name";
                        error_message += " and no valid file path and name is supplied";
                        throw new ArgumentException(error_message);
                    }
                }


                args.previous_searches = opts.previous_searches;
                if (sf_type.requires_prev_saf_ids)
                {
                    if (args.previous_searches.Count() == 0)
                    {
                        string error_message = "This search fetch type requires  one or more";
                        error_message += " previous search-fetch ids and none were supplied supplied";
                        throw new ArgumentException(error_message);
                    }
                }

                // Simply pass the 'No logging' boolean switch and
                // skip recent days nullable integer across
                // as well as any start / end pages

                args.no_logging = opts.no_logging;
                args.skip_recent_days = opts.skip_recent_days;
                args.start_page = opts.start_page;
                args.end_page = opts.end_page;

                // Create the main functional class and set it to work.

                Downloader dl = new Downloader(_monitor_repo, _logging_helper);
                await dl.RunDownloaderAsync(args, source);
                return 0;
            }

            catch (ArgumentException a)
            {
                if (_logging_helper.LogFilePath == null)
                {
                    // create a log file without the source parameter
                    _logging_helper.OpenNoSourceLogFile();

                }
                string error_header = "Parameter exception ERROR found:\n";
                string error_message = "Parameter exception: " + a.Message;
                string target_site = "Target Site: " + a.TargetSite.Name;
                string stack_trace = "Stack Trace:\n" + a.StackTrace;
                EndOnError(_logging_helper, error_header, error_message, target_site, stack_trace);
                return -1;
            }

            catch (Exception e)
            {
                string error_header = "Unhandled exception ERROR found:\n";
                string error_message = "Parameter exception: " + e.Message;
                string target_site = "Target Site: " + e.TargetSite.Name;
                string stack_trace = "Stack Trace:\n" + e.StackTrace;
                EndOnError(_logging_helper, error_header, error_message, target_site, stack_trace);
                return -1;
            }

             
                      
        }


        private static void EndOnError(LoggingHelper logging_helper, string error_header, string error_message,
                                        string target_site, string stack_trace)
        {
            logging_helper.LogCodeError("Uncaught Error in " + target_site, error_message, stack_trace);
            logging_helper.CloseLog();
        }


       

        static Task HandleParseErrorAsync(IEnumerable<Error> errs)
        {
            foreach (Error e in errs)
            {
                Console.WriteLine(e.Tag.ToString());
            }
            return Task.CompletedTask;
        }

    }


    public class Options
    {
        // Lists the command line arguments and options

        [Option('s', "source", Required = true, HelpText = "Integer id of data source.")]
        public int source_id { get; set; }

        [Option('t', "sf_type_id", Required = true, HelpText = "Integer id representing type of search / fetch.")]
        public int search_fetch_type_id { get; set; }

        [Option('f', "file_name", Required = false, HelpText = "Filename of csv file with data.")]
        public string file_name { get; set; }

        [Option('d', "cutoff_date", Required = false, HelpText = "Only data revised or added since this date will be considered")]
        public string cutoff_date { get; set; }

        [Option('q', "filter_id", Required = false, HelpText = "Integer id representing id of focused search / fetch.")]
        public int? focused_search_id { get; set; }

        [Option('I', "skip_recent", Required = false, HelpText = "Integer id representing the number of days ago, to skip recent downloads (0 = today).")]
        public int? skip_recent_days { get; set; }

        [Option('p', "previous_searches", Required = false, Separator = ',', HelpText = "One or more ids of the search(es) that will be used to retrieve the data")]
        public IEnumerable<int> previous_searches { get; set; }

        [Option('L', "no_Logging", Required = false, HelpText = "If present prevents the logging record in sf.saf_events")]
        public bool? no_logging { get; set; }

        [Option('S', "start_page", Required = false, HelpText = "First summary page number to be considered if downloading all EU CTR records (starts at 1)")]
        public int? start_page { get; set; }

        [Option('E', "end_page", Required = false, HelpText = "Last summary page number to be considered if downloading all EU CTR record")]
        public int? end_page { get; set; }

    }


    public class Args
    {
        public int source_id { get; set; }
        public int type_id { get; set; }
        public string file_name { get; set; }
        public DateTime? cutoff_date { get; set; }
        public int? filter_id { get; set; }
        public int? skip_recent_days { get; set; }
        public IEnumerable<int> previous_searches { get; set; }
        public bool? no_logging { get; set; }
        public int? start_page { get; set; }
        public int? end_page { get; set; }
    }

}



