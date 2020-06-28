using ScrapySharp.Network;
using System;
using System.Text;
using static System.Console;
using DataDownloader.yoda;
using DataDownloader.biolincc;
using DataDownloader.euctr;
using DataDownloader.isrctn;
using DataDownloader.who;
using DataDownloader.vivli;
using DataDownloader.pubmed;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DataDownloader
{
	class Program
	{
		static ScrapingBrowser browser = new ScrapingBrowser();

		static async Task Main(string[] args)
		{
			// Identify source type and location, destination folder

			if (NoArgsProvided(args)) return;
			int source_id = GetFirstArg(args[0]);
			if (source_id == 0) return;

			LoggingDataLayer logging_repo = new LoggingDataLayer();
			Source source = logging_repo.FetchSourceParameters(source_id);
			if (source == null)
			{
				WriteLine("Sorry - the first argument does not correspond to a known source");
				return;
			}

			int sf_id = logging_repo.GetNextSearchFetchId();

			int harvest_type_id = GetHarvestType(args, source);
			DateTime? harvest_cutoff_revision_date = harvest_type_id == 2 ? GetHarvestCutOffDate(args) : null;
			if (harvest_type_id == 2 && harvest_cutoff_revision_date == null)
			{
				WriteLine("Sorry - there must be a valid cutoff date for a harvest of type 2");
				return;
			}

			string source_file = "";
			if (source_id == 100115)   // needs changing to a source property by adding to table
            {
				// will be type 1 harvest (all of content) but will 
				// require a path to the file
				source_file = GetSourceFile(args);

			}


			browser.AllowAutoRedirect = true;
			browser.AllowMetaRedirect = true;
			browser.Encoding = Encoding.UTF8;

			switch (source.id)
			{
				case 101900:
					{
						BioLINCC_Controller biolincc_controller = new BioLINCC_Controller(browser, sf_id, source, logging_repo);
						biolincc_controller.LoopThroughPages();
						break;
					}
				case 101901:
					{
						Yoda_Controller yoda_controller = new Yoda_Controller(browser, sf_id, source, logging_repo);
						yoda_controller.LoopThroughPages();
						break;
					}
				case 100120:
					{
						break;
					}
				case 100123:
					{
						ISRCTN_Controller biolincc_controller = new ISRCTN_Controller(browser, sf_id, source, logging_repo);
						biolincc_controller.LoopThroughPages(); 
						break;
					}
				case 100126:
					{
						EUCTR_Controller biolincc_controller = new EUCTR_Controller(browser, sf_id, source, logging_repo);
						biolincc_controller.LoopThroughPages(); 
						break;
					}
				case 100115:
					{
						WHO_Controller who_controller = new WHO_Controller(source_file, sf_id, source, logging_repo);
						who_controller.ProcessFile();
						break;
					}
				case 100135:
					{
						break;
					}
				case 101940:
					{
						// vivli
						// second parameter to be added here to control exact functions used
						// and table creation etc.
						Vivli_Controller vivli_controller = new Vivli_Controller(browser, sf_id, source, logging_repo);
						vivli_controller.FetchURLDetails();
						vivli_controller.LoopThroughPages();
						break;
					}
			}

			// tidy up and ensure logging up to date
			// logging_repo.CreateSFLoggingRecord();
		}


		private static bool NoArgsProvided(string[] args)
		{
			if (args.Length == 0)
			{
				// may need a cut off point....
				WriteLine("Sorry - two parameters are necessary");
				WriteLine("The first is a 6 digit number to indicate the source.");
				WriteLine("The second an integer to indicate the last search-fetch id");
				return true;
			}
			else
			{
				return false;
			}
		}

		private static int GetFirstArg(string arg)
		{
			int arg_id = 0;
			if (!Int32.TryParse(arg, out arg_id))
			{
				WriteLine("Sorry - the first argument must be an integer");
			}
			return arg_id;
		}


		private static int GetHarvestType(string[] args, Source source_parameters)
		{
			if (args.Length > 1)
			{
				int harvest_type_arg = 0;
				if (!Int32.TryParse(args[1], out harvest_type_arg))
				{
					WriteLine("The second argument, if present, must be an integer (default settinmg will be used)");
					harvest_type_arg = source_parameters.default_harvest_type_id;
				}
				if (harvest_type_arg != 1 && harvest_type_arg != 2 && harvest_type_arg != 3)
				{
					WriteLine("Sorry - the second argument, if present, must be 1, 2, or 3 (default settinmg will be used)");
					harvest_type_arg = source_parameters.default_harvest_type_id;
				}
				return harvest_type_arg;
			}
			else
			{
				// use the default harvesting method
				return source_parameters.default_harvest_type_id;
			}
		}


		private static DateTime? GetHarvestCutOffDate(string[] args)
		{
			if (args.Length < 3)
			{
				WriteLine("Sorry - if the second argument is 2, ");
				WriteLine("(harvest only files revised after a set date)");
				WriteLine("You must include a third date parameter in the format YYYY-MM-DD");
				return null;
			}

			if (!Regex.Match(args[2], @"^20\d{2}-[0,1]\d{1}-[0, 1, 2, 3]\d{1}$").Success)
			{
				WriteLine("Sorry - if the second argument is 2, "); ;
				WriteLine("(harvest only files revised after a set date)");
				WriteLine("The third parameter must be in in the format YYYY-MM-DD");
				return null;
			}

			return new DateTime(Int32.Parse(args[2].Substring(0, 4)),
								Int32.Parse(args[2].Substring(5, 2)),
								Int32.Parse(args[2].Substring(8, 2)));
		}


		private static string GetSourceFile(string[] args)
		{
			if (args.Length < 3)
			{
				WriteLine("Sorry - For this source, ");
				WriteLine("(that uses a previously downloaded file as the data source)");
				WriteLine("You must include a fulklk poath to that file as the 3rd parameter");
				return null;
			}

			return args[2];
		}
	}
}



