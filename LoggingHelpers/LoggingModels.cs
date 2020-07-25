using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;

namespace DataDownloader
{
	[Table("sf.source_parameters")]
	public class Source
	{
		public int id { get; set; }
		public int? preference_rating { get; set; }
		public string database_name { get; set; }
		public bool uses_who_harvest { get; set; }
		public string local_folder { get; set; }
		public bool? local_files_grouped { get; set; }
		public int? grouping_range_by_id { get; set; }
		public string local_file_prefix { get; set; }
		public bool has_study_tables { get; set; }
		public bool has_study_topics { get; set; }
		public bool has_study_features { get; set; }
		public bool has_study_contributors { get; set; }
		public bool has_study_references { get; set; }
		public bool has_study_relationships { get; set; }
		public bool has_study_links { get; set; }
		public bool has_study_ipd_available { get; set; }
		public bool has_dataset_properties { get; set; }
		public bool uses_language_default { get; set; }
		public bool has_object_languages { get; set; }
		public bool has_object_dates { get; set; }
		public bool has_object_pubmed_set { get; set; }
	}


	[Table("sf.search_fetch_types")]
	public class SFType
	{
		public int id { get; set; }
		public string name { get; set; }
		public bool requires_date { get; set; }
		public bool requires_file { get; set; }
		public bool requires_search_id { get; set; }
		public bool requires_prev_sf_ids { get; set; }
		public string description { get; set; }
		public string list_order { get; set; }
	}
		

	[Table("sf.search_fetches")]
	public class SearchFetchRecord
	{
		[ExplicitKey]
		public int id { get; set; }
		public int source_id { get; set; }
		public int type_id { get; set; }
		public DateTime? time_started { get; set; }
		public DateTime? time_ended { get; set; }
		public int? num_records_checked { get; set; }
		public int? num_records_added { get; set; }
		public int? num_records_downloaded { get; set; }
		public int? focused_search_id { get; set; }
		public string comments { get; set; }
	}
	

	[Table("sf.source_data_studies")]
	public class StudyFileRecord
	{
		public int id { get; set; }
		public int source_id { get; set; }
		public string sd_id { get; set; }
		public string remote_url { get; set; }
		public int last_sf_id { get; set; }
		public DateTime? last_revised { get; set; }
		public bool? assume_complete { get; set; }
		public int download_status { get; set; }
		public DateTime? download_datetime { get; set; }
		public string local_path { get; set; }
		public DateTime? last_processed { get; set; }

		// constructor when a revision data can be expected (not always there)
		public StudyFileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
											  DateTime? _last_revised, string _local_path)
		{
			source_id = _source_id;
			sd_id = _sd_id;
			remote_url = _remote_url;
			last_sf_id = _last_sf_id;
			last_revised = _last_revised;
			download_status = 2;
			download_datetime = DateTime.Now;
			local_path = _local_path;
		}

		// constructor when an 'assumed complete' judgement can be expected (not always there)
		public StudyFileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
											  bool? _assume_complete, string _local_path)
		{
			source_id = _source_id;
			sd_id = _sd_id;
			remote_url = _remote_url;
			last_sf_id = _last_sf_id;
			assume_complete = _assume_complete;
			download_status = 2;
			download_datetime = DateTime.Now;
			local_path = _local_path;
		}


		public StudyFileRecord()
		{ }

	}


	[Table("sf.source_data_objects")]
	public class ObjectFileRecord
	{
		public int id { get; set; }
		public int source_id { get; set; }
		public string sd_id { get; set; }
		public string remote_url { get; set; }
		public int last_sf_id { get; set; }
		public DateTime? last_revised { get; set; }
		public bool? assume_complete { get; set; }
		public int download_status { get; set; }
		public DateTime? download_datetime { get; set; }
		public string local_path { get; set; }
		public DateTime? last_processed { get; set; }

		// constructor when a revision data can be expected (not always there)
		public ObjectFileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
											  DateTime? _last_revised, string _local_path)
		{
			source_id = _source_id;
			sd_id = _sd_id;
			remote_url = _remote_url;
			last_sf_id = _last_sf_id;
			last_revised = _last_revised;
			download_status = 2;
			download_datetime = DateTime.Now;
			local_path = _local_path;
		}

		// constructor when an 'assumed complete' judgement can be expected (not always there)
		public ObjectFileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
											  bool? _assume_complete, string _local_path)
		{
			source_id = _source_id;
			sd_id = _sd_id;
			remote_url = _remote_url;
			last_sf_id = _last_sf_id;
			assume_complete = _assume_complete;
			download_status = 2;
			download_datetime = DateTime.Now;
			local_path = _local_path;
		}

		public ObjectFileRecord()
		{ }

	}

	public class DownloadResult
	{
		public int num_checked { get; set; }
		public int num_added { get; set; }
		public int num_downloaded { get; set; }

		public DownloadResult()
        {
			num_checked = 0;
			num_added = 0;
			num_downloaded = 0;
		}
	}


	public class PMCResponse
	{
		public string status { get; set; }
		public string responseDate { get; set; }
		public string request { get; set; }
		public List<NLMRecords> records { get; set; }
	}


	public class NLMRecords
	{
		public string pmcid { get; set; }
		public string pmid { get; set; }
		public string doi { get; set; }
	}

}
