using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;

namespace DataDownloader
{
	[Table("sf.source_data_studies")]
	public class FileRecord
	{
		[Key]
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
		public FileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
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
		public FileRecord(int _source_id, string _sd_id, string _remote_url, int _last_sf_id,
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

		public FileRecord()
		{ }

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
