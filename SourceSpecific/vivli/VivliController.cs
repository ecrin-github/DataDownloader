using ScrapySharp.Network;
using System;
using System.Collections.Generic;

namespace DataDownloader.vivli
{
    public class Vivli_Controller
    {
        ScrapingBrowser _browser;
        VivliDataLayer _vivli_repo;
        Vivli_Processor _processor;
        Source _source;
        string _file_base;
        FileWriter _file_writer;
        int _saf_id;
        int _source_id;
        MonitorDataLayer _monitor_repo;
        LoggingHelper _logging_helper;


        public Vivli_Controller(ScrapingBrowser browser, int saf_id, Source source, Args args, MonitorDataLayer monitor_repo, LoggingHelper logging_helper)
        {
            _browser = browser;
            _vivli_repo = new VivliDataLayer();
            _processor = new Vivli_Processor();
            _source = source;
            _file_base = source.local_folder;
            _source_id = source.id;
            _saf_id = saf_id;
            _file_writer = new FileWriter(source);
            _monitor_repo = monitor_repo;
            _logging_helper = logging_helper;
        }


        public void FetchURLDetails()
        {
            VivliCopyHelpers vch = new VivliCopyHelpers();
            
            // Set up initial study list
            // store it in pp table

            List<VivliURL> all_study_list = new List<VivliURL>();
            _vivli_repo.SetUpParameterTable();

            string baseURL = "https://search.datacite.org/works?query=vivli&resource-type-id=dataset";
            WebPage startPage = _browser.NavigateToPage(new Uri(baseURL));

            // Entries on DataCite search are 25 / page
            int totalNumber = _processor.GetStudyNumbers(startPage);
            int loopEndNumber = (totalNumber / 25) + 2;

            // for (int i = 1; i < 5; i++)  // testing only
            for (int i = 1; i < loopEndNumber; i++)
            {
                string URL = baseURL + " &page=" + i.ToString();
                WebPage web_page = _browser.NavigateToPage(new Uri(URL));

                List<VivliURL> page_study_list = _processor.GetStudyInitialDetails(web_page, i);
                _vivli_repo.StoreRecs(vch.api_url_copyhelper, page_study_list);

                // Log to console and pause before the next page

                _logging_helper.LogLine(i.ToString());
                System.Threading.Thread.Sleep(1000);
            }
        }

        public void LoopThroughPages()
        {

            // Go through the vivli data, fetcvhing the stored urls
            // and using these to call the api directly, receiving json
            // that can be extracted directly from the response

            _vivli_repo.SetUpStudiesTable();
            _vivli_repo.SetUpPackagesTable();
            _vivli_repo.SetUpDataObectsTable();

            IEnumerable<VivliURL> all_study_list = _vivli_repo.FetchVivliApiUrLs();

            foreach (VivliURL s in all_study_list)
            {
                _processor.GetAndStoreStudyDetails(s, _vivli_repo, _logging_helper);

                // logging to go here

                // write to console...
                _logging_helper.LogLine(s.id.ToString() + ": " + s.vivli_url);

                // put a pause here if necessary
                System.Threading.Thread.Sleep(800);

            }
        }
    }
}
