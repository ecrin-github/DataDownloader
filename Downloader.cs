using ScrapySharp.Network;
using System;
using System.Text;
using DataDownloader.ctg;
using DataDownloader.yoda;
using DataDownloader.biolincc;
using DataDownloader.euctr;
using DataDownloader.isrctn;
using DataDownloader.who;
using DataDownloader.vivli;
using DataDownloader.pubmed;
using System.Linq;

namespace DataDownloader
{

	public class Downloader
	{
		ScrapingBrowser browser;

		public Downloader()
        {
			// Set up browser fro scraping.

			browser = new ScrapingBrowser();
			browser.AllowAutoRedirect = true;
			browser.AllowMetaRedirect = true;
			browser.Encoding = Encoding.UTF8;
		}

		public void RunDownloader(Args args, Source source)
		{

			// Identify source type and location, destination folder

			Console.WriteLine("source_id is " + args.source_id.ToString());
			Console.WriteLine("type_id is " + args.type_id.ToString());
			Console.WriteLine("file_name is " + args.file_name);
			Console.WriteLine("cutoff_date is " + args.cutoff_date);
			if (args.previous_searches.Count() > 0)
			{
				foreach (int i in args.previous_searches)
				{
					Console.WriteLine("previous_search is " + i.ToString());
				}
			}
			Console.WriteLine("no_Logging is " + args.no_logging);


			LoggingDataLayer logging_repo = new LoggingDataLayer();
			int sf_id = logging_repo.GetNextSearchFetchId();
    		string source_file = args.file_name;

			// Set up search-fetch record

			SearchFetchRecord sfr = new SearchFetchRecord();
			sfr.id = sf_id;
			sfr.source_id = source.id;
			sfr.type_id = args.type_id; 
			sfr.focused_search_id = args.focused_search_id; 
			sfr.time_started = DateTime.Now;

			DownloadResult res = new DownloadResult();

			switch (source.id)
			{
				case 101900:
					{
						BioLINCC_Controller biolincc_controller = new BioLINCC_Controller(browser, sf_id, source, args, logging_repo);
						res = biolincc_controller.LoopThroughPages();
						break;
					}
				case 101901:
					{
						Yoda_Controller yoda_controller = new Yoda_Controller(browser, sf_id, source, args, logging_repo);
						res = yoda_controller.LoopThroughPages();
						break;
					}
				case 100120:
					{
						CTG_Controller ctg_controller = new CTG_Controller(browser, sf_id, source, args, logging_repo);
						res = ctg_controller.LoopThroughPages();
						break;
					}
				case 100123:
					{
						ISRCTN_Controller isrctn_controller = new ISRCTN_Controller(browser, sf_id, source, args, logging_repo);
						res = isrctn_controller.LoopThroughPages(); 
						break;
					}
				case 100126:
					{
						EUCTR_Controller euctr_controller = new EUCTR_Controller(browser, sf_id, source, args, logging_repo);
						res = euctr_controller.LoopThroughPages(); 
						break;
					}
				case 100115:
					{
						WHO_Controller who_controller = new WHO_Controller(source_file, sf_id, source, args, logging_repo);
						res = who_controller.ProcessFile();
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
						Vivli_Controller vivli_controller = new Vivli_Controller(browser, sf_id, source, args, logging_repo);
						vivli_controller.FetchURLDetails();
						vivli_controller.LoopThroughPages();
						break;
					}
			}

			// tidy up and ensure logging up to date
			// logging_repo.CreateSFLoggingRecord();
			sfr.time_ended = DateTime.Now;
			sfr.num_records_checked = res.num_checked;
			sfr.num_records_downloaded = res.num_downloaded;
			sfr.num_records_added = res.num_added;
			if (!args.no_logging)
            {
				// Store the sfr log record.
				logging_repo.InsertSFLogRecord(sfr);
			}
		}

	}
}



