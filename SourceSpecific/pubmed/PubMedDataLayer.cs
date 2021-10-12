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
        private LoggingDataLayer logging_repo;

        /// <summary>
        /// Parameterless constructor is used to automatically build
        /// the connection string, using an appsettings.json file that 
        /// has the relevant credentials (but which is not stored in GitHub).
        /// The json file also includes the root folder path, which is
        /// stored in the class's folder_base property.
        /// </summary>
        /// 
        public PubMedDataLayer(LoggingDataLayer _logging_repo)
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

            logging_repo = _logging_repo;
        }

        // Tables and functions used for the PMIDs collected from DB Sources

        public void SetUpTempPMIDsBySourceTables()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"DROP TABLE IF EXISTS pp.pmids_by_source_total;
                       CREATE TABLE IF NOT EXISTS pp.pmids_by_source_total(
                        source_id int
                      , sd_sid varchar
                      , pmid varchar)";
                conn.Execute(sql_string);

                sql_string = @"DROP TABLE IF EXISTS pp.distinct_pmids;
                       CREATE TABLE IF NOT EXISTS pp.distinct_pmids(
                         identity int GENERATED ALWAYS AS IDENTITY
                       , group_id int
                       , pmid varchar)";
                conn.Execute(sql_string);

                sql_string = @"DROP TABLE IF EXISTS pp.pmid_id_strings;
                       CREATE TABLE IF NOT EXISTS pp.pmid_id_strings(
                       id_string varchar)";
                conn.Execute(sql_string);
            }
        }


        public IEnumerable<Source> FetchSourcesWithReferences()
        {
            using (var conn = new NpgsqlConnection(mon_connString))
            {
                string sql_string = @"select * from sf.source_parameters
                where has_study_references = true";
                return conn.Query<Source>(sql_string);
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


        public void CreatePMID_IDStrings()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"INSERT INTO pp.distinct_pmids(
                          pmid)
                          SELECT DISTINCT pmid
                          FROM pp.pmids_by_source_total
                          ORDER BY pmid;";
                conn.Execute(sql_string);

                sql_string = @"Update pp.distinct_pmids
                          SET group_id = identity / 100;";
                conn.Execute(sql_string);

                // fill the id list (100 ids in each striong)
                sql_string = @"INSERT INTO pp.pmid_id_strings(
                        id_string)
                        SELECT DISTINCT string_agg(pmid, ', ') 
                        OVER (PARTITION BY group_id) 
                        from pp.distinct_pmids;";
                conn.Execute(sql_string);
            }
        }

        public IEnumerable<string> FetchSourcePMIDStrings()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"select id_string
                        from pp.pmid_id_strings;";
                return conn.Query<string>(sql_string);
            }
        }

        public void DropPMIDSourceTempTables()
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                string sql_string = @"DROP TABLE IF EXISTS pp.pmids_by_source_total;
                    DROP TABLE IF EXISTS pp.distinct_pmids;";
                conn.Execute(sql_string);
            }
        }


        public IEnumerable<PMSource> FetchDatabanks()
        {
            using (NpgsqlConnection Conn = new NpgsqlConnection(context_connString))
            {
                string SQLString = "select id, nlm_abbrev from ctx.nlm_databanks where id not in (100156, 100157, 100158)";
                return Conn.Query<PMSource>(SQLString);
            }
        }	

    }

}


