using Dapper.Contrib.Extensions;
using PostgreSQLCopyHelper;
using System;
using System.Collections.Generic;

namespace DataDownloader.pubmed
{
	public class PubMedRecord
	{
		// largely a copy of a biolincc record - needs replacing
		// In fact the PubMed record basically transferred without analysis
		// simply split into separate articvles from a list of 10...

		// when operating off the back of a search the selection of
		// the individual; ids, and id strings, will be different
		// but otherwise the porocess remains the same


		public string sd_oid { get; set; }
		public string remote_url { get; set; }
		public string display_title { get; set; }
		public string public_title { get; set; }
		public string acronym { get; set; }
		public int? study_type_id { get; set; }
		public string study_type { get; set; }
		public string brief_description { get; set; }
		public string study_period { get; set; }
		public string date_prepared { get; set; }
		public DateTime? page_prepared_date { get; set; }
		public string last_updated { get; set; }
		public DateTime? last_revised_date { get; set; }
		public int publication_year { get; set; }
		public string study_website { get; set; }
		public int num_clinical_trial_urls { get; set; }
		public int num_primary_pub_urls { get; set; }
		public int num_associated_papers { get; set; }
		public string resources_available { get; set; }
		public int dataset_consent_type_id { get; set; }
		public string dataset_consent_type { get; set; }
		public string dataset_consent_restrictions { get; set; }

		public PubMedRecord()
		{}
    }

	public class PMSource
	{
		public int id { get; set; }

		public string default_name { get; set; }

		public string nlm_abbrev { get; set; }
	}


	public class FoundResult
	{
		public int pmid { get; set; }

		public int bank_id { get; set; }

		public int sf_id { get; set; }

		public FoundResult(int _pmid, int _bank_id)
		{
			pmid = _pmid;
			bank_id = _bank_id;
			sf_id = 100008;
		}
	}


	public class FetchRecord
	{
		public int pmid { get; set; }

		public string file_path { get; set; }

		public DateTime date_last_fetched { get; set; }

		public DateTime? date_last_revised { get; set; }

		public FetchRecord(int _pmid, string _file_path, 
			DateTime _date_last_fetched, DateTime? _date_last_revised)
		{
			pmid = _pmid;
			file_path = _file_path;
			date_last_fetched = _date_last_fetched;
			date_last_revised = _date_last_revised;
		}
	}

	
	public class pmid_holder
	{
		public string pmid { get; set; }
	}

	public class CopyHelpers
	{
		// defines the copy helpers required.
		// see https://github.com/PostgreSQLCopyHelper/PostgreSQLCopyHelper for details

		public PostgreSQLCopyHelper<pmid_holder> pubmed_ids_helper =
			 new PostgreSQLCopyHelper<pmid_holder>("pp", "temp_pmid_by_source")
				 .MapVarchar("pmid", x => x.pmid);

		public PostgreSQLCopyHelper<FoundResult> found_result_copyhelper =
			new PostgreSQLCopyHelper<FoundResult>("pp", "temp_pmid_by_bank")
				.MapInteger("pmid", x => x.pmid)
				.MapInteger("bank_id", x => x.bank_id);

		/*

		public PostgreSQLCopyHelper<FetchRecord> pmid_fetch_copyhelper =
			new PostgreSQLCopyHelper<FetchRecord>("pp", "pmid_fetches")
				.MapInteger("pmid", x => x.pmid)
				.MapVarchar("file_path", x => x.file_path)
				.MapDate("date_last_fetched", x => x.date_last_fetched)
				.MapDate("date_last_revised", x => x.date_last_revised);
		*/
	}


}