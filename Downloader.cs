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
using System.Threading.Tasks;

namespace DataDownloader
{

	public class Downloader
	{
		ScrapingBrowser browser;

		public Downloader()
        {
			// Set up browser for scraping.

			browser = new ScrapingBrowser();
			browser.AllowAutoRedirect = true;
			browser.AllowMetaRedirect = true;
			browser.Encoding = Encoding.UTF8;
		}

		public async Task RunDownloaderAsync(Args args, Source source)
		{
			// Identify source type and location, destination folder
			StringHelpers.SendHeader("Set up");
			StringHelpers.SendFeedback("source_id is " + args.source_id.ToString());
			StringHelpers.SendFeedback("type_id is " + args.type_id.ToString());
			StringHelpers.SendFeedback("file_name is " + args.file_name);
			StringHelpers.SendFeedback("cutoff_date is " + args.cutoff_date);

			string previous_saf_ids = null;
			if (args.previous_searches.Count() > 0)
			{
				foreach (int i in args.previous_searches)
				{
					StringHelpers.SendFeedback("previous_search is " + i.ToString());
					previous_saf_ids += ", " + i.ToString();
				}
				previous_saf_ids = previous_saf_ids.Substring(2);
			}
			StringHelpers.SendFeedback("no_Logging is " + args.no_logging);


			LoggingDataLayer logging_repo = new LoggingDataLayer();
			int saf_id = logging_repo.GetNextSearchFetchId();
    		string source_file = args.file_name;

			// Set up search and fetch record
			SAFEvent saf = new SAFEvent(saf_id, source.id, args.type_id, args.filter_id, args.cutoff_date, previous_saf_ids);

			DownloadResult res = new DownloadResult();

			switch (source.id)
			{
				case 101900:
					{
						BioLINCC_Controller biolincc_controller = new BioLINCC_Controller(browser, saf_id, source, args, logging_repo);
						res = biolincc_controller.LoopThroughPages();
						break;
					}
				case 101901:
					{
						Yoda_Controller yoda_controller = new Yoda_Controller(browser, saf_id, source, args, logging_repo);
						res = yoda_controller.LoopThroughPages();
						break;
					}
				case 100120:
					{
						CTG_Controller ctg_controller = new CTG_Controller(browser, saf_id, source, args, logging_repo);
						res = ctg_controller.LoopThroughPages();
						break;
					}
				case 100126:
					{
						ISRCTN_Controller isrctn_controller = new ISRCTN_Controller(browser, saf_id, source, args, logging_repo);
						res = isrctn_controller.LoopThroughPages(); 
						break;
					}
				case 100123:
					{
						EUCTR_Controller euctr_controller = new EUCTR_Controller(browser, saf_id, source, args, logging_repo);
						res = euctr_controller.LoopThroughPages(); 
						break;
					}
				case 100115:
					{
						WHO_Controller who_controller = new WHO_Controller(source_file, saf_id, source, args, logging_repo);
						res = who_controller.ProcessFile();
						break;
					}
				case 100135:
					{
						// PubMed
						// See notes at top of PubMed controller for explanation of different download types
						PubMed_Controller pubmed_controller = new PubMed_Controller(saf_id, source, args, logging_repo);
						res = await pubmed_controller.ProcessDataAsync();
						break;
					}
				case 101940:
					{
						// vivli
						// second parameter to be added here to control exact functions used
						// and table creation etc.
						Vivli_Controller vivli_controller = new Vivli_Controller(browser, saf_id, source, args, logging_repo);
						vivli_controller.FetchURLDetails();
						vivli_controller.LoopThroughPages();
						break;
					}
			}

			// tidy up and ensure logging up to date

			saf.time_ended = DateTime.Now;
			saf.num_records_checked = res.num_checked;
			saf.num_records_downloaded = res.num_downloaded;
			saf.num_records_added = res.num_added;
			if (!args.no_logging)
            {
				// Store the saf log record.
				logging_repo.InsertSAFEventRecord(saf);
			}
		}
	}
}



