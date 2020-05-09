using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DataDownloader
{
    public class BioLINCC_Controller
    {
		ScrapingBrowser browser;
		DataLayer repo;
		BioLinccDataLayer biolincc_repo;
		BioLINCC_Processor processor;
		string file_base;
		FileWriter file_writer;
		int last_sf_id;
		int source_id;

		public BioLINCC_Controller(ScrapingBrowser _browser, DataLayer _repo, int _last_sf_id, int _source_id)
		{
			browser = _browser;
			repo = _repo;
			biolincc_repo = new BioLinccDataLayer();
			file_base = repo.GetBioLinccFolderBase();
			processor = new BioLINCC_Processor();
			file_writer = new FileWriter(_repo);
			last_sf_id = _last_sf_id;
			source_id = _source_id;
		}


		public void LoopThroughPages()
		{
			// biolincc start page
			WebPage homePage = browser.NavigateToPage(new Uri("https://biolincc.nhlbi.nih.gov/studies/"));

			int study_id = 1000;  // arbitrary start value
			var study_list_table = homePage.Find("div", By.Class("table-responsive"));
			HtmlNode[] studyRows = study_list_table.CssSelect("tbody tr").ToArray();
			XmlSerializer writer = new XmlSerializer(typeof(BioLinccRecord));

			foreach (HtmlNode row in studyRows)
			{
				study_id++;
				// if (study_id < 1048) continue;  // continuing after a break	
				// if (study_id > 1010) break;     // testing

				// fetch the constructed study record
				BioLinccRecord st = processor.GetStudyDetails(browser, biolincc_repo, study_id, row);

				if (st != null)
				{
					// Write out study record as XML
					string file_name = st.sd_id + ".xml";
					string full_path = Path.Combine(file_base, file_name);
					file_writer.WriteBioLINCCFile(writer, st, full_path);
					file_writer.UpdateDownloadLog(study_id, source_id, st.sd_id, st.remote_url, last_sf_id,
													  st.last_revised_date, full_path);

					// put a pause here if necessary
					System.Threading.Thread.Sleep(1000);
				}
			}
		}
    }
}
