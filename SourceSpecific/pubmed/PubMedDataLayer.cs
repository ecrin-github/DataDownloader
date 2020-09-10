using Dapper.Contrib.Extensions;
using Dapper;
using Npgsql;
using System;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using PostgreSQLCopyHelper;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Xml.Serialization;
using System.IO;
using System.Threading.Tasks;

namespace DataDownloader.pubmed
{
	public class PubMedDataLayer
	{
		NpgsqlConnectionStringBuilder builder;
		private string connString;
		private string mon_connString;

		private string context_connString;
		private string folder_base;

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

			builder = new NpgsqlConnectionStringBuilder();
			builder.Host = settings["host"];
			builder.Username = settings["user"];
			builder.Password = settings["password"];
			builder.Database = "pubmed";

			connString = builder.ConnectionString;

			builder.Database = "mon";
			mon_connString = builder.ConnectionString;

			builder.Database = "context";
			context_connString = builder.ConnectionString;

			folder_base = settings["folder_base"];
		}


		// Tables and functions used for the PMIDs collected from DB Sources

		public IEnumerable<Source> FetchSourcesWithReferences()
		{
			using (var conn = new NpgsqlConnection(mon_connString))
			{
				string sql_string = @"select * from sf.source_parameters
                where has_study_references = true";
				return conn.Query<Source>(sql_string);
			}
		}

        public void SetUpTempPMIDsBySourceTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"DROP TABLE IF EXISTS pp.temp_pmid_by_source;
                        CREATE TABLE IF NOT EXISTS pp.temp_pmid_by_source(
				        pmid varchar) ";
				conn.Execute(sql_string);
			}
		}

		public void SetUpSourcePMIDsTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"DROP TABLE IF EXISTS pp.pmids_by_source_total;
                       CREATE TABLE IF NOT EXISTS pp.pmids_by_source_total(
				         source_id int
                       , pmid varchar)";
				conn.Execute(sql_string);
			}
		}		

		public void TruncateTempPMIDsBySourceTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"TRUNCATE TABLE pp.temp_pmid_by_source";
				conn.Execute(sql_string);
			}
		}

        public IEnumerable<pmid_holder> FetchReferences(string db_name)
		{
			builder.Database = db_name;
			string db_conn_string = builder.ConnectionString;

			using (var conn = new NpgsqlConnection(db_conn_string))
			{
				string sql_string = @"SELECT DISTINCT pmid from ad.study_references 
				        where pmid is not null";
				return conn.Query<pmid_holder>(sql_string);
			}
		}
		
		public ulong StorePmidsBySource(PostgreSQLCopyHelper<pmid_holder> copyHelper, 
			         IEnumerable<pmid_holder> entities)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				conn.Open();
				return copyHelper.SaveAll(conn, entities);
			}
		}
		
		public void TransferSourcePMIDsToTotalTable(int source_id)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"INSERT INTO pp.pmids_by_source_total(
				          source_id, pmid) 
                          SELECT " + source_id.ToString() + @", t.pmid
						  FROM pp.temp_pmid_by_source;";
				conn.Execute(sql_string);
			}
		}

		public IEnumerable<pmid_holder> FetchDistinctSourcesPMIDs()
        {
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"select distinct pmid 
						  FROM pp.pmids_by_source_total;";
				return conn.Query<pmid_holder>(sql_string);
			}
		}

        public int ObtainTotalOfPMIDSBySource()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"SELECT COUNT(*) FROM pp.pmids_by_source_total";
				return conn.ExecuteScalar<int>(sql_string);
			}
		}


		// Tables and functions used for the PMIDs collected from Pubmed Bank references

        public IEnumerable<PMSource> FetchDatabanks()
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection(context_connString))
			{
				string SQLString = "select id, nlm_abbrev from ctx.nlm_databanks where bank_type = 'Trial registry'";
				return Conn.Query<PMSource>(SQLString);
			}
		}		

        public void SetUpTempPMIDsByBankTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"DROP TABLE IF EXISTS pp.temp_pmids_by_bank;
                     CREATE TABLE IF NOT EXISTS pp.temp_pmids_by_bank(
                        pmid varchar)";
				conn.Execute(sql_string);
			}
		}

		public void SetUpBankPMIDsTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"DROP TABLE IF EXISTS pp.pmids_by_bank_total;
                       CREATE TABLE IF NOT EXISTS pp.pmids_by_bank_total(
				         bank_id int
                       , pmid varchar)";
				conn.Execute(sql_string);
			}
		}
		
		public async Task<int> GetDataCountAsync(string url)
		{
			try
			{
				HttpClient webClient = new HttpClient();
				string responseBody = await webClient.GetStringAsync(url);
				XmlSerializer xSerializer = new XmlSerializer(typeof(eSearchResult));
				using (TextReader reader = new StringReader(responseBody))
				{
					// The eSearchResult class was generated automaticaly using the xsd.exe tool

					eSearchResult result = (eSearchResult)xSerializer.Deserialize(reader);
					if (result != null)
					{
						return result.Count;
					}
					else
					{
						return 0;
					}
				}
			}

			catch (HttpRequestException e)
			{
				Console.WriteLine("\nException Caught!");
				Console.WriteLine("Message :{0} ", e.Message);
				return 0;
			}
		}
		
		public void TruncateTempPMIDsByBankTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"TRUNCATE TABLE pp.temp_pmid_by_bank";
				conn.Execute(sql_string);
			}
		}

		public ulong StorePMIDsByBank(PostgreSQLCopyHelper<FoundResult> copyHelper,
					 IEnumerable<FoundResult> entities)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				conn.Open();
				return copyHelper.SaveAll(conn, entities);
			}
		}


		public void TransferBankPMIDsToTotalTable(string bank_abbrev)
		{
			using (var conn = new NpgsqlConnection(mon_connString))
			{
				string sql_string = @"INSERT INTO pp.pmids_by_bank_total(
				          bank_id, pmid) 
				          SELECT " + bank_abbrev + @", pmid
						  FROM pp.temp_pmids_by_bank";
				conn.Execute(sql_string);
			}
		}

		public IEnumerable<pmid_holder> FetchDistinctBanksPMIDs()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = @"select distinct pmid 
						  FROM pp.pmids_by_bank_total;";
				return conn.Query<pmid_holder>(sql_string);
			}
		}

		public void DropTempPMIDBySourceTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "DROP TABLE IF EXISTS pp.temp_pmid_by_source";
				conn.Execute(sql_string);
			}
		}


		public void DropTempPMIDCollectorTable()
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				string sql_string = "DROP TABLE IF EXISTS pp.temp_pmid_collector";
				conn.Execute(sql_string);
			}
		}


		// Returns the total number of PubMed Ids to be processd

		public int GetSourceRecordCount()
		{
			using (var conn = new NpgsqlConnection(mon_connString))
			{
				string query_string = @"SELECT COUNT(*) FROM sf.source_data_objects 
                                WHERE source_id = 100135 AND download_status = 0";
				return conn.ExecuteScalar<int>(query_string);
			}
		}


		// This function does an initial query of the PubMed API to see how many records there 
		// are altogether, to fetch in the following loop - MOVE to PubMed Repo

		


		// Returns the total number of PubMed Ids

		public int GetTotalRecordCount()
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection(mon_connString))
			{
				string query_string = @"SELECT COUNT(*) FROM sf.source_data_objects 
                                WHERE source_id = 100135";
				return Conn.ExecuteScalar<int>(query_string);
			}
		}



		// Uses the string_agg function in Postgres to return the 10 Ids 
		// being retrieved as a comma delimited string.

		public string FetchIdString(int skip)
		{
			using (var conn = new NpgsqlConnection(mon_connString))
			{
				string query_string = @"select STRING_AGG(recs.pmid, ',') FROM 
				                     (SELECT sd_id as pmid FROM sf.source_data_objects 
								     WHERE source_id = 100135 AND download_status = 0 
                                     ORDER BY sd_id OFFSET " + skip.ToString() + @" limit 10) recs";
				return conn.ExecuteScalar<string>(query_string);
			}
		}


		// stores the ids and titles obtained from the API call

		public ulong StorePMIDList(PostgreSQLCopyHelper<FoundResult> copyHelper, IEnumerable<FoundResult> entities)
		{
			using (var conn = new NpgsqlConnection(connString))
			{
				conn.Open();
				return copyHelper.SaveAll(conn, entities);
			}
		}

		/*
		public ObjectFileRecord FetchRecordByPMID(string pmid)
		{
			using (NpgsqlConnection Conn = new NpgsqlConnection(mon_connString))
			{
				string query_string = "select id, source_id, sd_id, remote_url, remote_lastsf_id, remote_last_revised, ";
				query_string += " download_status, download_datetime, local_path, local_last_revised ";
				query_string += " from sf.object_source_data where sd_id = '" + pmid + "';";
				return Conn.Query<ObjectFileRecord>(query_string).FirstOrDefault();
			}
		}
		*/

	}

}


