using Dapper.Contrib.Extensions;
using PostgreSQLCopyHelper;
using System;
using System.Collections.Generic;

namespace DataDownloader.pubmed
{
	
	public class PMSource
	{
		public int id { get; set; }

		public string default_name { get; set; }

		public string nlm_abbrev { get; set; }
	}


	public class PMIDBySource
	{
		public string sd_sid { get; set; }
		public string pmid { get; set; }
	}

	public class PMIDByBank
	{
		public string pmid { get; set; }

		public PMIDByBank( string _pmid)
		{
			pmid = _pmid;
		}
	}
	
	
	public class CopyHelpers
	{
		// defines the copy helpers required.
		// see https://github.com/PostgreSQLCopyHelper/PostgreSQLCopyHelper for details

		public PostgreSQLCopyHelper<PMIDBySource> source_ids_helper =
				new PostgreSQLCopyHelper<PMIDBySource>("pp", "temp_pmids_by_source")
			        .MapVarchar("sd_sid", x => x.sd_sid)
					.MapVarchar("pmid", x => x.pmid);

		public PostgreSQLCopyHelper<PMIDByBank> bank_ids_helper =
			new PostgreSQLCopyHelper<PMIDByBank>("pp", "temp_pmids_by_bank")
				    .MapVarchar("pmid", x => x.pmid);
	}
}