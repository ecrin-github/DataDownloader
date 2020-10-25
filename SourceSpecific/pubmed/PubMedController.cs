using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace DataDownloader.pubmed
{

    // Pubmed searches and / or file downloads are often associated with filters...
    // 10001	"PubMed CTG"	"PubMed abstracts with references to ClinicalTrials.gov entries"
    // 10002	"PubMed COVID"	"PubMed abstracts found on searches related to COVID-19 (SARS, MERS etc.)"
    // 10003	"PubMed Registries"	"PubMed abstracts with references to any trial registry"
    // 10004	"Pubmed - Study References"	"Identifies PubMed references in Study sources that have not yet been downloaded"

    // Pubmed data has two sources - the study references in certain databases
    // and the pubmed records themselves - specifically those that have a reference to a 'databank' 
    // (trial registry in this context)

    // Normally, the 'bank' pmid records will be identified by an initial search, but include only those 
    // that have been modified since the last similar download (represented by the cutoff date). 
    // This includes new records.

    // The records found for each bank are stored in a table in the pp schema - pp.temp_by_bank_records.
    // These are transferred in turn to the pp.pmids_with_bank_records table

    // At the end of the loop through all banks the *distinct* pmid records are transfered to a list in memory.
    // The system loops through these - if the record exists it is replaced and the object source record
    // is updated in the mon database.
    // if the record is new the record is downloaded and a new object source record is created.
    // This strategy is represented by saf_type 114 (with cutoff date, and filter 10003)

    // A variant allows all pmids with bank records to bne downloaded, irrespective of date, but otherwise 
    // the process is the same.
    // This strategy is represented by saf_type 121 Filtered records (download) (with filter 10003)

    // The study_reference records are collected - one database at a time - and transferred to a single database.
    // At the end of that process a distinct list is created in memory.
    // The system loops through this 10 records at a time. If there is no object source record one is created 
    // and the file is downloaded - it is new to the system.
    // If a source record exists the record needs checking to see if it has been revised since the last similar exercise.
    // If it has the record is downloaded and the source file is updated. If not, no download takes place. 
    // This strategy is represented by saf_type 114 (with cutoff date, and filter 10004)

    // A variant allows all pmids derived from study references, irrespective of revision date, to be downloaded.
    // This strategy is represented by saf_type 121 Filtered records (download) (with filter 10004)

    public class PubMed_Controller
    {
        HttpClient webClient;
        LoggingDataLayer logging_repo;
        PubMedDataLayer pubmed_repo;
        Source source;
        FileWriter file_writer;
        int saf_id;
        int source_id;
        Args args;
        XmlWriterSettings settings;

        public PubMed_Controller(int _saf_id, Source _source, Args _args, LoggingDataLayer _logging_repo)
        {
            saf_id = _saf_id;
            args = _args;
            logging_repo = _logging_repo;

            source = _source;
            source_id = source.id;
            file_writer = new FileWriter(source);
            
            pubmed_repo = new PubMedDataLayer();
            webClient = new HttpClient();
            settings = new XmlWriterSettings();
            settings.Async = true;
            settings.Encoding = System.Text.Encoding.UTF8;
        }

        public async Task<DownloadResult> ProcessDataAsync()
        {
            DownloadResult res = null;
            if (args.type_id == 114 && args.filter_id == 10003)
            {
                // download pmids with references to trial registries, that
                // have been revised since the cutoff date
                await CreatePMIDsListfromBanksAsync();
                IEnumerable<string> idstrings = pubmed_repo.FetchDistinctBankPMIDStrings();
                res = await DownloadPubmedEntriesAsync(idstrings);
            }
            if (args.type_id == 114 && args.filter_id == 10004)
            {
                // download pmids listed as references in other sources,
                // that have been revised since the cutoff date
                CreatePMIDsListfromSources();
                IEnumerable<string> idstrings = pubmed_repo.FetchDistinctSourcePMIDStrings();

                res = await DownloadPubmedEntriesAsync(idstrings);
            }
            if (args.type_id == 121 && args.filter_id == 10003)
            {
                // download all pmids with references to trial registries
                await CreatePMIDsListfromBanksAsync();
                IEnumerable<string> idstrings = pubmed_repo.FetchDistinctBankPMIDStrings();
                res = await DownloadPubmedEntriesAsync(idstrings);
            }
            if (args.type_id == 121 && args.filter_id == 10004)
            {
                // download all pmids listed as references in other sources
                CreatePMIDsListfromSources();
                IEnumerable<string> idstrings = pubmed_repo.FetchDistinctSourcePMIDStrings();
                res = await DownloadPubmedEntriesAsync(idstrings);
            }
            return res;
        }

        public void CreatePMIDsListfromSources()
        {
            // Establish tables and support objects

            pubmed_repo.SetUpTempPMIDsBySourceTable();
            pubmed_repo.SetUpSourcePMIDsTable();
            pubmed_repo.SetUpDistinctSourcePMIDsTable();
            CopyHelpers helper = new CopyHelpers();
            IEnumerable<PMIDBySource> references;

            // Loop threough the study databases that hold
            // study_reference tables, i.e. with pmid ids
            IEnumerable<Source> sources = pubmed_repo.FetchSourcesWithReferences();
            foreach (Source s in sources)
            {
                pubmed_repo.TruncateTempPMIDsBySourceTable();
                references = pubmed_repo.FetchSourceReferences(s.database_name);
                pubmed_repo.StorePmidsBySource(helper.source_ids_helper, references);
                pubmed_repo.TransferSourcePMIDsToTotalTable(s.id);
            }

            pubmed_repo.FillDistinctSourcePMIDsTable();
            pubmed_repo.DropTempPMIDBySourceTable();
        }


        public async Task CreatePMIDsListfromBanksAsync()
        {
            int totalRecords, numCallsNeeded, bank_id;
            int retmax = 1000;
            string search_term;
            CopyHelpers helper = new CopyHelpers();
            string baseURL = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&term=";
            bool include_dates = (args.type_id == 114) ? true : false;

            pubmed_repo.SetUpTempPMIDsByBankTable();
            pubmed_repo.SetUpBankPMIDsTable();
            pubmed_repo.SetUpDistinctBankPMIDsTable();

            // Get list of potential linked data banks (includes trial registries)
            IEnumerable<PMSource> banks = pubmed_repo.FetchDatabanks();

            foreach (PMSource s in banks)
            {
                // get databank details
                bank_id = s.id;
                search_term = s.nlm_abbrev + "[SI]";
                if (include_dates)
                {
                    string today = DateTime.Now.ToString("yyyy/MM/dd");
                    string cutoff = ((DateTime)args.cutoff_date).ToString("yyyy/MM/dd");
                    string date_term = "&mindate=" + cutoff + "&maxdate=" + today + "&datetype=mdat";
                    search_term += date_term;
                }
                string url = baseURL + search_term;


                // Get the number of total records that have this databank reference
                // and calculate the loop parameters
                totalRecords = await pubmed_repo.GetBankDataCountAsync(url);
                numCallsNeeded = (int)(totalRecords / retmax) + 1;
                pubmed_repo.TruncateTempPMIDsByBankTable();

                // loop through the records and obtain and store relevant
                // records retmax (= 1000) at a time

                for (int i = 0; i < numCallsNeeded; i++)
                {
                    try
                    {
                        int start = i * 1000;
                        string selectedRecords = "&retstart=" + start.ToString() + "&retmax=" + retmax.ToString();

                        // Put a 2 second pause beefore each call.
                        await Task.Delay(2000);
                        string responseBody = await webClient.GetStringAsync(url + selectedRecords);

                        // The eSearchResult class allows the returned json string to be easily deserialised
                        // and the required values, of each Id in the IdList, can then be read.

                        XmlSerializer xSerializer = new XmlSerializer(typeof(eSearchResult));
                        using (TextReader reader = new StringReader(responseBody))
                        {
                            eSearchResult result = (eSearchResult)xSerializer.Deserialize(reader);
                            if (result != null)
                            {
                                var FoundIds = Array.ConvertAll(result.IdList, ele => new PMIDByBank(ele.ToString()));
                                pubmed_repo.StorePMIDsByBank(helper.bank_ids_helper, FoundIds);

                                string feedback = "Storing " + retmax.ToString() +
                                                  " records from " + start.ToString() + " in bank " + search_term;
                                StringHelpers.SendFeedback(feedback);
                            }
                        }
                    }

                    catch (HttpRequestException e)
                    {
                        StringHelpers.SendError("In PubMed CreatePMIDsListfromBanksAsync(): " + e.Message);
                    }
                }

                // transfer across to total table...
                pubmed_repo.TransferBankPMIDsToTotalTable(s.nlm_abbrev);
            }

            pubmed_repo.DropTempPMIDByBankTable();
            pubmed_repo.FillDistinctBankPMIDsTable();
        }


        public async Task<DownloadResult> DownloadPubmedEntriesAsync(IEnumerable<string> idstrings)
        {
            string baseURL = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?";
            baseURL += "tool=ECRINMDR&email=steve.canham@ecrin.org&db=pubmed&id=";
            DownloadResult res = new DownloadResult();
            bool ignore_revision_date = (args.type_id == 121) ? true : false;

            try
            {
                // loop through the references - already in groups of 10 
                // from processing in the database call

                foreach (string idstring in idstrings)
                {
                    // Construct the fetch URL using the 10 Ids and
                    // retrieve the articles as nodes

                    string url = baseURL + idstring + "&retmode=xml";
                    XmlDocument xdoc = new XmlDocument();
                    string responseBody = await webClient.GetStringAsync(url);
                    xdoc.LoadXml(responseBody);
                    XmlNodeList articles = xdoc.GetElementsByTagName("PubmedArticle");

                    // Consider each article node in turn

                    foreach (XmlNode article in articles)
                    {
                        string pmid = article.SelectSingleNode("MedlineCitation/PMID").InnerText;
                        if (Int32.TryParse(pmid, out int ipmid))
                        {
                            // get current or new file download record, calculate
                            // and store last revised date. Write new or replace
                            // file and update file_record (by ref).
                            res.num_checked++;
                            DateTime? last_revised_datetime = GetDateLastRevised(article);
                            ObjectFileRecord file_record = logging_repo.FetchObjectFileRecord(pmid, source.id);
                            if (file_record == null)
                            {
                                string remote_url = "https://www.ncbi.nlm.nih.gov/pubmed/" + pmid;
                                file_record = new ObjectFileRecord(source.id, pmid, remote_url, saf_id);
                                file_record.last_revised = last_revised_datetime;
                                WriteNewFile(article, ipmid, file_record);
                                
                                logging_repo.InsertObjectFileRec(file_record);
                                res.num_added++;
                                res.num_downloaded++;
                            }
                            else
                            {
                                // normally should be less then but here <= to be sure
                                if (ignore_revision_date || 
                                   (last_revised_datetime != null 
                                           && file_record.last_downloaded <= last_revised_datetime))
                                {
                                    file_record.last_saf_id = saf_id;
                                    file_record.last_revised = last_revised_datetime;
                                    ReplaceFile(article, file_record);

                                    logging_repo.StoreObjectFileRec(file_record);
                                    res.num_downloaded++;
                                }
                            }

                            if (res.num_checked % 100 == 0) StringHelpers.SendFeedback(res.num_checked.ToString());
                        }
                    }

                    System.Threading.Thread.Sleep(800);
                }

                return res;
            }

            catch (HttpRequestException e)
            {
                StringHelpers.SendError("In PubMed DownloadPubmedEntriesUsingSourcesAsync(): " + e.Message);
                return res;
            }
        }


        private DateTime? GetDateLastRevised(XmlNode article)
        {
            DateTime? date_last_revised = null;

            string year = article.SelectSingleNode("MedlineCitation/DateRevised/Year").InnerText ?? "";
            string month = article.SelectSingleNode("MedlineCitation/DateRevised/Month").InnerText ?? "";
            string day = article.SelectSingleNode("MedlineCitation/DateRevised/Day").InnerText ?? "";

            if (year != "" && month != "" && day != "")
            {
                if (Int32.TryParse(year, out int iyear) 
                && Int32.TryParse(month, out int imonth)
                && Int32.TryParse(day, out int iday))
                {
                    date_last_revised = new DateTime(iyear, imonth, iday);
                }
            }
            return date_last_revised;
        }


        private void WriteNewFile(XmlNode article, int ipmid, ObjectFileRecord file_record)
        {
            string folder_name = Path.Combine(source.local_folder, "PM" + (ipmid / 10000).ToString("00000") + "xxxx");
            if (!Directory.Exists(folder_name))
            {
                Directory.CreateDirectory(folder_name);
            }
            string filename = "PM" + ipmid.ToString("000000000") + ".xml";
            string full_path = Path.Combine(folder_name, filename);

            using (XmlWriter writer = XmlWriter.Create(full_path, settings))
            {
                article.WriteTo(writer);
            }

            file_record.local_path = full_path;
            file_record.download_status = 2;
            file_record.last_downloaded = DateTime.Now;
        }


        private void ReplaceFile(XmlNode article, ObjectFileRecord file_record)
        {
            string full_path = file_record.local_path;
            // ensure can over write
            if (File.Exists(full_path))
            {
                File.Delete(full_path);
            }
            using (XmlWriter writer = XmlWriter.Create(full_path, settings))
            {
                article.WriteTo(writer);
            }
            file_record.last_downloaded = DateTime.Now;
        }

    }
}
