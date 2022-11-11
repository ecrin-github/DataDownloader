using DataDownloader.biolincc;
using DataDownloader.ctg;
using DataDownloader.euctr;
using DataDownloader.isrctn;
using DataDownloader.pubmed;
using DataDownloader.vivli;
using DataDownloader.who;
using DataDownloader.yoda;
using ScrapySharp.Network;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataDownloader
{

    public class Downloader
    {
        ScrapingBrowser browser;
        MonitorDataLayer monitor_repo;
        LoggingHelper logging_helper;

        public Downloader(MonitorDataLayer _monitor_repo, LoggingHelper _logging_helper)
        {
            // Set up browser for scraping.

            browser = new ScrapingBrowser();
            browser.AllowAutoRedirect = true;
            browser.AllowMetaRedirect = true;
            browser.Encoding = Encoding.UTF8;

            monitor_repo = _monitor_repo;
            logging_helper = _logging_helper;
        }

        public async Task RunDownloaderAsync(Args args, Source source)
        {
            // Log parameters and set up search and fetch record.
            string previous_saf_ids = logging_helper.LogArgsParameters(args);
            int saf_id = monitor_repo.GetNextSearchFetchId();
            string download_comments = "";
            SAFEvent saf = new SAFEvent(saf_id, source.id, args.type_id, args.filter_id, args.cutoff_date, previous_saf_ids);

            DownloadResult res = new DownloadResult();

            switch (source.id)
            {
                case 101900:
                    {
                        BioLINCC_Controller biolincc_controller = new BioLINCC_Controller(browser, saf_id, source, args, monitor_repo, logging_helper);
                        res = biolincc_controller.LoopThroughPages();
                        biolincc_controller.PostProcessData();
                        break;
                    }
                case 101901:
                    {
                        Yoda_Controller yoda_controller = new Yoda_Controller(browser, saf_id, source, args, monitor_repo, logging_helper);
                        res = yoda_controller.LoopThroughPages();
                        break;
                    }
                case 100120:
                    {
                        CTG_Controller ctg_controller = new CTG_Controller(saf_id, source, args, monitor_repo, logging_helper);
                        res = await ctg_controller.ProcessDataAsync();
                        break;
                    }
                case 100126:
                    {
                        ISRCTN_Controller isrctn_controller = new ISRCTN_Controller(browser, saf_id, source, args, monitor_repo, logging_helper);
                        res = isrctn_controller.LoopThroughPages(); 
                        break;
                    }
                case 100123:
                    {
                        EUCTR_Controller euctr_controller = new EUCTR_Controller(browser, saf_id, source, args, monitor_repo, logging_helper);
                        res = euctr_controller.LoopThroughPages(); 
                        break;
                    }
                case 100115:
                    {
                        download_comments = "Source file:" + args.file_name;
                        WHO_Controller who_controller = new WHO_Controller(saf_id, source, args, monitor_repo, logging_helper);
                        res = who_controller.ProcessFile();
                        break;
                    }
                case 100135:
                    {
                        // PubMed
                        // See notes at top of PubMed controller for explanation of different download types
                        PubMed_Controller pubmed_controller = new PubMed_Controller(saf_id, source, args, monitor_repo, logging_helper);
                        res = await pubmed_controller.ProcessDataAsync();
                        break;
                    }
                case 101940:
                    {
                        // vivli
                        // second parameter to be added here to control exact functions used
                        // and table creation etc.
                        Vivli_Controller vivli_controller = new Vivli_Controller(browser, saf_id, source, args, monitor_repo, logging_helper);
                        vivli_controller.FetchURLDetails();
                        vivli_controller.LoopThroughPages();
                        break;
                    }
            }

            // tidy up and ensure logging up to date

            saf.time_ended = DateTime.Now;
            if (res != null)
            {
                saf.num_records_checked = res.num_checked;
                saf.num_records_downloaded = res.num_downloaded;
                saf.num_records_added = res.num_added;
                if (download_comments != "")
                {
                    saf.comments = download_comments;
                }
                logging_helper.LogRes(res);
            }
            if (args.no_logging == null || args.no_logging == false)
            {
                // Store the saf log record.
                monitor_repo.InsertSAFEventRecord(saf);
            }
            logging_helper.CloseLog();
        }
    }
}



