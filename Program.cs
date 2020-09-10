using System;
using CommandLine;
using static System.Console;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DataDownloader
{
	class Program
	{
		static void Main(string[] args)
		{
			var result = Parser.Default.ParseArguments<Options>(args)
			.WithParsed(RunOptions)
			.WithNotParsed(HandleParseError);
		}

		static void RunOptions(Options opts)
		{
			// Handle options.

			Args args = new Args();
            LoggingDataLayer logging_repo = new LoggingDataLayer();

			// Check source id is valid. 
			
			Source source = logging_repo.FetchSourceParameters(opts.source_id);
			if (source == null)
			{
				WriteLine("Sorry - the first argument does not correspond to a known source");
				return;
			}
			args.source_id = source.id;


			// Check source fetch type id is valid. 

			SFType sf_type = logging_repo.FetchTypeParameters(opts.search_fetch_type_id);
			if (sf_type == null)
			{
				WriteLine("Sorry - the type argument does not correspond to a known search / fetch type");
				return;
			}
			args.type_id = sf_type.id;


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
				
				if (args.cutoff_date == null)
                {
     				WriteLine("Sorry - this search fetch type requires a date"); ;
					WriteLine("in the format YYYY-MM-DD and this is missing");
					return;
				}
			}

			// If a file is required check a name is supplied and that it
			// corresponds to a file.

			args.file_name = opts.file_name;
			if (sf_type.requires_file)
			{
				if (string.IsNullOrEmpty(args.file_name) || !File.Exists(args.file_name))
				{
					WriteLine("Sorry - this search fetch type requires a file name"); ;
					WriteLine("and no valid file path and name is supplied");
					return;
				}
			}

			args.filter_id = opts.focused_search_id;
			if (sf_type.requires_search_id)
			{
				if (args.filter_id == 0 || args.filter_id == null)
				{
					WriteLine("Sorry - this search fetch type requires an integer referencing a search type"); ;
					WriteLine("and no valid file path and name is supplied");
					return;
				}
			}


			args.previous_searches = opts.previous_searches;
			if (sf_type.requires_prev_saf_ids)
			{
				if (args.previous_searches.Count() == 0)
				{
					WriteLine("Sorry - this search fetch type requires one or more"); ;
					WriteLine("previous search-fetch ids and none were supplied supplied");
					return;
				}
			}

			// Simply pass the 'No logging' boolean switch across

			args.no_logging = opts.no_logging;

			Downloader dl = new Downloader();
			dl.RunDownloaderAsync(args, source);
		}

		static void HandleParseError(IEnumerable<Error> errs)
		{
			// handle errors
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
		public int focused_search_id { get; set; }

		[Option('p', "previous_searches", Required = false, Separator = ',', HelpText = "One or more ids of the search(es) that will be used to retrieve the data")]
		public IEnumerable<int> previous_searches { get; set; }

		[Option('L', "no_Logging", Required = false, HelpText = "If present prevents the logging record in sf.search_fetches")]
		public bool no_logging { get; set; }

	}


	public class Args
	{
		public int source_id { get; set; }
		public int type_id { get; set; }
		public string file_name { get; set; }
		public DateTime? cutoff_date { get; set; }
		public int? filter_id { get; set; }
		public IEnumerable<int> previous_searches { get; set; }
		public bool no_logging { get; set; }
	}

}



