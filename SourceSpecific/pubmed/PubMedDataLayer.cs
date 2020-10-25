using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PostgreSQLCopyHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

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
                string sql_string = @"DROP TABLE IF EXISTS pp.temp_pmids_by_source;
                        CREATE TABLE IF NOT EXISTS pp.temp_pmids_by_source(
                        sd_sid varchar
                      , pmid varchar) ";
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
                      , sd_sid varchar
                      , pmid varchar)";
                conn.Execute(sql_string);
            }
        }

        public void SetUpDistinctSourcePMIDsTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"DROP TABLE IF EXISTS pp.distinct_pmids_by_source;
                       CREATE TABLE IF NOT EXISTS pp.distinct_pmids_by_source(
                         identity int GENERATED ALWAYS AS IDENTITY
                       , group_id int
                       , pmid varchar)";
                conn.Execute(sql_string);
            }
        }

        public void TruncateTempPMIDsBySourceTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"TRUNCATE TABLE pp.temp_pmids_by_source";
                conn.Execute(sql_string);
            }
        }

        public IEnumerable<PMIDBySource> FetchSourceReferences(string db_name)
        {
            builder.Database = db_name;
            string db_conn_string = builder.ConnectionString;

            using (var conn = new NpgsqlConnection(db_conn_string))
            {
                string sql_string = @"SELECT DISTINCT 
                        sd_sid, pmid from ad.study_references 
                        where pmid is not null and pmid <> ''";
                return conn.Query<PMIDBySource>(sql_string);
            }
        }
        
        public ulong StorePmidsBySource(PostgreSQLCopyHelper<PMIDBySource> copyHelper, 
                     IEnumerable<PMIDBySource> entities)
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
                          source_id, sd_sid, pmid) 
                          SELECT " + source_id.ToString() + @", sd_sid, pmid
                          FROM pp.temp_pmids_by_source;";
                conn.Execute(sql_string);
            }
        }

        public void FillDistinctSourcePMIDsTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"INSERT INTO pp.distinct_pmids_by_source(
                          pmid)
                          SELECT DISTINCT pmid
                          FROM pp.pmids_by_source_total
                          ORDER BY pmid;";
                conn.Execute(sql_string);

                sql_string = @"Update pp.distinct_pmids_by_source
                          SET group_id = identity / 10;";
                conn.Execute(sql_string);
            }
        }

        public IEnumerable<string> FetchDistinctSourcePMIDStrings()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"select distinct string_agg(pmid, ', ') 
                        OVER ( PARTITION BY group_id) 
                        from pp.distinct_pmids_by_source;";
                return conn.Query<string>(sql_string);
            }
        }

        public void DropTempPMIDBySourceTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = "DROP TABLE IF EXISTS pp.temp_pmids_by_source";
                conn.Execute(sql_string);
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
                         bank_name varchar
                       , pmid varchar)";
                conn.Execute(sql_string);
            }
        }

        public void SetUpDistinctBankPMIDsTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"DROP TABLE IF EXISTS pp.distinct_pmids_by_bank;
                       CREATE TABLE IF NOT EXISTS pp.distinct_pmids_by_bank(
                         identity int GENERATED ALWAYS AS IDENTITY
                       , group_id int
                       , pmid varchar)";
                conn.Execute(sql_string);
            }
        }

        public async Task<int> GetBankDataCountAsync(string url)
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
                StringHelpers.SendError("In PubMed GetBankDataCountAsync: "+ e.Message);
                return 0;
            }
        }
        
        public void TruncateTempPMIDsByBankTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"TRUNCATE TABLE pp.temp_pmids_by_bank";
                conn.Execute(sql_string);
            }
        }

        public ulong StorePMIDsByBank(PostgreSQLCopyHelper<PMIDByBank> copyHelper,
                     IEnumerable<PMIDByBank> entities)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();
                return copyHelper.SaveAll(conn, entities);
            }
        }


        public void TransferBankPMIDsToTotalTable(string bank_abbrev)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"INSERT INTO pp.pmids_by_bank_total(
                          bank_name, pmid) 
                          SELECT '" + bank_abbrev + @"', pmid
                          FROM pp.temp_pmids_by_bank";
                conn.Execute(sql_string);
            }
        }

        public void FillDistinctBankPMIDsTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"INSERT INTO pp.distinct_pmids_by_bank(
                          pmid)
                          SELECT DISTINCT pmid
                          FROM pp.pmids_by_bank_total
                          ORDER BY pmid;";
                conn.Execute(sql_string);

                sql_string = @"Update pp.distinct_pmids_by_bank
                          SET group_id = identity / 10;";
                conn.Execute(sql_string);
            }
        }

        public IEnumerable<string> FetchDistinctBankPMIDStrings()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"select distinct string_agg(pmid, ', ') 
                        OVER ( PARTITION BY group_id) 
                        from pp.distinct_pmids_by_bank;";
                return conn.Query<string>(sql_string);
            }
        }


        public IEnumerable<string> FetchDistinctMissingPMIDStrings()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"select distinct string_agg(pmid, ', ') 
                        OVER (PARTITION BY group_id) 
                        from pp.missing_pmids_grouped;";
                return conn.Query<string>(sql_string);
            }
        }
        

        public void DropTempPMIDByBankTable()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = "DROP TABLE IF EXISTS pp.temp_pmids_by_bank";
                conn.Execute(sql_string);
            }
        }

    }

}


