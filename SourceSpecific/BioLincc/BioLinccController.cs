using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader.biolincc
{
    public class BioLINCC_Controller
    {
		ScrapingBrowser browser;
		BioLinccDataLayer biolincc_repo;
		BioLINCC_Processor processor;
		Source source;
		string file_base;
		FileWriter file_writer;
		int sf_id;
		int source_id;
		LoggingDataLayer logging_repo;

		public BioLINCC_Controller(ScrapingBrowser _browser, int _sf_id, Source _source, LoggingDataLayer _logging_repo)
		{
			browser = _browser;
			biolincc_repo = new BioLinccDataLayer();
			processor = new BioLINCC_Processor();
			source = _source;
			file_base = source.local_folder;
			source_id = source.id;
			sf_id = _sf_id;
			file_writer = new FileWriter(source);
			logging_repo = _logging_repo;
		}


		public void LoopThroughPages()
		{
			// biolincc start page
			WebPage homePage = browser.NavigateToPage(new Uri("https://biolincc.nhlbi.nih.gov/studies/"));

			int seqnum = 1000;  // arbitrary start value
			var study_list_table = homePage.Find("div", By.Class("table-responsive"));
			HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();
			XmlSerializer writer = new XmlSerializer(typeof(BioLinccRecord));

			foreach (HtmlNode row in studyRows)
			{
				seqnum++;
				// if (study_id < 1048) continue;  // continuing after a break	
				// if (study_id > 1010) break;     // testing

				// fetch the constructed study record
				BioLinccRecord st = processor.GetStudyDetails(browser, biolincc_repo, seqnum, row);

				if (st != null)
				{
					// Write out study record as XML
					string file_name = source.local_file_prefix + st.sd_sid + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteBioLINCCFile(writer, st, full_path);
					logging_repo.UpdateDownloadLog(seqnum, source_id, st.sd_sid, st.remote_url, sf_id,
													  st.last_revised_date, full_path);

					// put a pause here if necessary
					System.Threading.Thread.Sleep(1000);
				}
			}
		}
    }
}
