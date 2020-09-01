using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Html;
using ScrapySharp.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace DataDownloader.pubmed
{
    public class PubMed_Controller
    {
		HttpClient webClient;
		LoggingDataLayer logging_repo;
		PubMedDataLayer pubmed_repo;
		Source source;
		string folder_base;
		FileWriter file_writer;
		int saf_id;
		int source_id;

		public PubMed_Controller(int _saf_id, Source _source, Args args, LoggingDataLayer _logging_repo)
		{
			webClient = new HttpClient();
			logging_repo = _logging_repo;
			pubmed_repo = new PubMedDataLayer();
			source = _source;
			folder_base = source.local_folder;
			source_id = source.id;
			file_writer = new FileWriter(source);
			saf_id = _saf_id;
			source_id = source.id;
		}


		public int CalcPMIDList()
		{
			// examines the study_reference data in the trial registry databases
			// to try and identify the PubMed data that needs to be downloaded through the API

			pubmed_repo.SetUpTempPMIDBySourceTable();
			pubmed_repo.SetUpTempPMIDCollectorTable();
			CopyHelper helper = new CopyHelper();
			IEnumerable<pmid_holder> references;

			// get study reference data from BioLINCC
			pubmed_repo.TruncatePMIDBySourceTable();
			references = pubmed_repo.FetchReferences("biolincc");
			pubmed_repo.StorePmids(helper.pubmed_ids_helper, references);
			pubmed_repo.TransferPMIDsToCollectorTable(100900);

			// get study reference data from Yoda
			pubmed_repo.TruncatePMIDBySourceTable();
			references = pubmed_repo.FetchReferences("yoda");
			pubmed_repo.StorePmids(helper.pubmed_ids_helper, references);
			pubmed_repo.TransferPMIDsToCollectorTable(100901);

			// get study reference data from ISRCTN
			pubmed_repo.TruncatePMIDBySourceTable();
			references = pubmed_repo.FetchReferences("isrctn");
			pubmed_repo.StorePmids(helper.pubmed_ids_helper, references);
			pubmed_repo.TransferPMIDsToCollectorTable(100126);

			// get study reference data from EUCTR
			pubmed_repo.TruncatePMIDBySourceTable();
			references = pubmed_repo.FetchReferences("euctr");
			pubmed_repo.StorePmids(helper.pubmed_ids_helper, references);
			pubmed_repo.TransferPMIDsToCollectorTable(100123);

			// get study reference data from ClinicalTrials.gov
			pubmed_repo.TruncatePMIDBySourceTable();
			references = pubmed_repo.FetchReferences("ctg");
			pubmed_repo.StorePmids(helper.pubmed_ids_helper, references);
			pubmed_repo.TransferPMIDsToCollectorTable(100120);

			// store the contents in the data objects source file as required...
			int total = pubmed_repo.ObtainTotalOfNewPMIDS();
			pubmed_repo.TransferNewPMIDsToSourceDataTable(saf_id);

			pubmed_repo.DropTempPMIDBySourceTable();
			pubmed_repo.DropTempPMIDCollectorTable();
			return total;
		}


		public async Task DownloadPagesAsync()
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Async = true;
			settings.Encoding = System.Text.Encoding.UTF8;

			// get list of remote urls from sf.source_data_objects
			int DataRecordCount = 0;
			DataRecordCount = pubmed_repo.GetSourceRecordCount();

			// Fetch the result set (10 citations from a pre-fetched list of databank related records).
			string baseURL = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?tool=ECRINMDR&email=steve@canhamis.eu&db=pubmed&id=";
			string folder_name, filename, full_path;

			// loop through 10 at a time
			for (int i = 0; i < DataRecordCount; i += 10)
			{
				// if (i > 50) break; // when testing...
				string idString = pubmed_repo.FetchIdString(i);
				if (idString == null || idString == "") break;

				// Fetch 10 PubMed records as XML.

				string url = baseURL + idString + "&retmode=xml";
				XmlDocument xdoc = new XmlDocument();
				string responseBody = await webClient.GetStringAsync(url);
				xdoc.LoadXml(responseBody);
				XmlNodeList articles = xdoc.GetElementsByTagName("PubmedArticle");

				// For each article node, use the xmlwriter to create a file
				// But also get last revised date in each case

				foreach (XmlNode article in articles)
				{
					string pmid = article.SelectSingleNode("MedlineCitation/PMID").InnerText;

					if (Int32.TryParse(pmid, out int ipmid))
					{
						// Construct / extract the dates required for logging

						DateTime? date_last_revised = null;
						DateTime date_last_fetched = DateTime.Now;

						string year = article.SelectSingleNode("MedlineCitation/DateRevised/Year").InnerText ?? "";
						string month = article.SelectSingleNode("MedlineCitation/DateRevised/Month").InnerText ?? "";
						string day = article.SelectSingleNode("MedlineCitation/DateRevised/Day").InnerText ?? "";

						if (year != "" && month != "" && day != "")
						{
							if (Int32.TryParse(year, out int iyear) && Int32.TryParse(month, out int imonth) && Int32.TryParse(day, out int iday))
							{
								date_last_revised = new DateTime(iyear, imonth, iday);
							}
						}

						// get current record in file download table
						ObjectFileRecord file_record = logging_repo.FetchObjectFileRecord(pmid, 100135);

                        folder_name = Path.Combine(folder_base, "PM" + (ipmid / 10000).ToString("00000") + "xxxx");
						filename = "PM" + ipmid.ToString("000000000") + ".xml";
						full_path = Path.Combine(folder_name, filename);

						if (file_record.download_status == 0)
						{
							using (XmlWriter writer = XmlWriter.Create(full_path, settings))
							{
								article.WriteTo(writer);
							}
							if (file_record.download_status == 0)
							{
								file_record.download_status = 2;
								file_record.last_revised = date_last_revised;
								file_record.last_downloaded = DateTime.Now;
								file_record.local_path = full_path;
							}
						}
						else
						{
							// normally should be less then but here <= to be sure
							if (date_last_revised != null && file_record.last_downloaded <= date_last_revised)
							{
								full_path = file_record.local_path;
								// ensure can over write
								if (File.Exists(full_path))
								{
									File.Delete(full_path);
								}
								using (XmlWriter writer = XmlWriter.Create(full_path, settings))
								{
									article.WriteTo(writer);
								}
								file_record.last_revised = date_last_revised;
								file_record.last_downloaded = DateTime.Now;
							}
						}
						file_record.last_saf_id = saf_id;
						logging_repo.StoreObjectFileRec(file_record);
					}
				}
				
				System.Threading.Thread.Sleep(1200);
				Console.WriteLine(i.ToString());
			}

		}
    }
}
