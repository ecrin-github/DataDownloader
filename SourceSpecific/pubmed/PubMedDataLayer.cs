using Dapper.Contrib.Extensions;
using Dapper;
using Npgsql;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using PostgreSQLCopyHelper;
using System.Net.Http.Headers;

namespace DataDownloader.pubmed
{
	public class PubMedDataLayer
	{
		private string biolincc_ad_connString;
		private string yoda_ad_connString;
		private string ctg_ad_connString;
		private string euctr_ad_connString;
		private string isrctn_ad_connString;
		private string mon_sf_connString;

		/// <summary>
		/// Parameterless constructor is used to automatically build
		/// the connection string, using an appsettings.json file that 
		/// has the relevant credentials (but which is not stored in GitHub).
		/// The json file also includes the root folder path, which is
		/// stored in the class's folder_base property.
		/// </summary>
		/// 
		public PubMedDataLayer()
		{
			IConfigurationRoot settings = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddJsonFile("appsettings.json")
				.Build();

			NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder();
			builder.Host = settings["host"];
			builder.Username = settings["user"];
			builder.Password = settings["password"];

			builder.Database = "biolincc";
			builder.SearchPath = "ad";
			biolincc_ad_connString = builder.ConnectionString;

			builder.Database = "ctg";
			builder.SearchPath = "ad";
			ctg_ad_connString = builder.ConnectionString;

			builder.Database = "yoda";
			builder.SearchPath = "ad";
			yoda_ad_connString = builder.ConnectionString;

			builder.Database = "euctr";
			builder.SearchPath = "ad";
			euctr_ad_connString = builder.ConnectionString;

			builder.Database = "isrctn";
			builder.SearchPath = "ad";
			isrctn_ad_connString = builder.ConnectionString;

			builder.Database = "mon";
			builder.SearchPath = "sf";
			mon_sf_connString = builder.ConnectionString;


			// example appsettings.json file...
			// the only values required are for...
			// {
			//	  "host": "host_name...",
			//	  "user": "user_name...",
			//    "password": "user_password...",
			//	  "folder_base": "C:\\MDR JSON\\Object JSON... "
			// }
		}


		public IEnumerable<pmid_holder> FetchReferences(string source )
		{
			string conn_string = "";
			switch (source)
			{
				case "biolincc": {conn_string = biolincc_ad_connString; break; }
				case "yoda": {conn_string = yoda_ad_connString; break; }
				case "isrctn": {conn_string = isrctn_ad_connString; break; }
				case "euctr": { conn_string = euctr_ad_connString; break; }
				case "ctg": {conn_string = ctg_ad_connString; break; }
			}

			using (var conn = new NpgsqlConnection(conn_string))
			{
				string sql_string = @"SELECT DISTINCT pmid from ad.study_references 
				        where pmid is not null";
				return conn.Query<pmid_holder>(sql_string);
			}
		}


		public void SetUpTempPMIDBySourceTable()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"CREATE TABLE IF NOT EXISTS sf.temp_pmid_by_source(
				        pmid varchar) ";
				conn.Execute(sql_string);
			}
		}

		public void TruncatePMIDBySourceTable()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"TRUNCATE TABLE sf.temp_pmid_by_source";
				conn.Execute(sql_string);
			}
		}

		public void SetUpTempPMIDCollectorTable()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"CREATE TABLE IF NOT EXISTS sf.temp_pmid_collector(
				         source_id int
                       , pmid varchar)";
				conn.Execute(sql_string);
			}
		}

		public void TransferPMIDsToCollectorTable(int source_id)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"INSERT INTO sf.temp_pmid_collector(
				          source_id, pmid) 
                          SELECT " + source_id.ToString()  + @", t.pmid
						  FROM sf.temp_pmid_by_source t
                          LEFT JOIN 
                               (select sd_id from sf.source_data_objects
                                where source_id = 100135) s
                          ON t.pmid = s.sd_id
                          WHERE s.sd_id is null";
				conn.Execute(sql_string);
			}
		}

		public int ObtainTotalOfNewPMIDS()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"SELECT COUNT(*) FROM sf.temp_pmid_collector";
				return conn.ExecuteScalar<int>(sql_string);
			}
		}

		public void TransferNewPMIDsToSourceDataTable(int last_saf_id)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = @"INSERT INTO sf.source_data_objects(
				          source_id, sd_id, remote_url, last_saf_id, download_status) 
				          SELECT 100135, pmid, 'https://www.ncbi.nlm.nih.gov/pubmed/' || pmid, "
						  + last_saf_id.ToString() + @", 0
						  FROM sf.temp_pmid_collector";
				conn.Execute(sql_string);
			}
		}

		public void DropTempPMIDBySourceTable()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = "DROP TABLE IF EXISTS sf.temp_pmid_by_source";
				conn.Execute(sql_string);
			}
		}

		public void DropTempPMIDCollectorTable()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string sql_string = "DROP TABLE IF EXISTS sf.temp_pmid_collector";
				conn.Execute(sql_string);
			}
		}

		public ulong StorePmids(PostgreSQLCopyHelper<pmid_holder> copyHelper, IEnumerable<pmid_holder> entities)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				conn.Open();
				return copyHelper.SaveAll(conn, entities);
			}
		}


		// Returns the total number of PubMed Ids to be processd

		public int GetSourceRecordCount()
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string query_string = @"SELECT COUNT(*) FROM sf.source_data_objects 
                                WHERE source_id = 100135 AND download_status = 0";
				return conn.ExecuteScalar<int>(query_string);
			}
		}


		// Uses the string_agg function in Postgres to retuen the 10 Ids 
		// being retrieved as a comma delimited string.

		public string FetchIdString(int skip)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string query_string = @"select STRING_AGG(recs.pmid, ',') FROM 
				                     (SELECT sd_id as pmid FROM sf.source_data_objects 
								     WHERE source_id = 100135 AND download_status = 0 
                                     ORDER BY sd_id OFFSET " + skip.ToString() + @" limit 10) recs";
				return conn.ExecuteScalar<string>(query_string);
			}
		}

		/*
		public ObjectFileRecord FetchRecordByPMID(string pmid)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				string query_string = "select id, source_id, sd_id, remote_url, lastsaf_id, remote_last_revised, ";
				query_string += " download_status, download_datetime, local_path, local_last_revised ";
				query_string += " from sf.object_source_data where sd_id = '" + pmid + "';";
				return conn.Query<ObjectFileRecord>(query_string).FirstOrDefault();
			}
		}

		public void Update_Record(ObjectFileRecord file_record)
		{
			using (var conn = new NpgsqlConnection(mon_sf_connString))
			{
				conn.Update<ObjectFileRecord>(file_record);
			}
		}

	*/

	}


	public class pmid_holder
	{
		public string pmid { get; set; }
	}


	public class CopyHelper
	{
	       public PostgreSQLCopyHelper<pmid_holder> pubmed_ids_helper =
				new PostgreSQLCopyHelper<pmid_holder>("sf", "temp_pmid_by_source")
					.MapVarchar("pmid", x => x.pmid); 
					 
	}

}

