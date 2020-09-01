using PostgreSQLCopyHelper;

namespace DataDownloader
{
    public static class LoggingCopyHelper
    {
        public static PostgreSQLCopyHelper<StudyFileRecord> file_record_copyhelper =
            new PostgreSQLCopyHelper<StudyFileRecord>("sf", "source_data_studies")
                .MapInteger("source_id", x => x.source_id)
                .MapVarchar("sd_id", x => x.sd_id)
                .MapVarchar("remote_url", x => x.remote_url)
                .MapInteger("last_saf_id", x => x.last_saf_id)
                .MapTimeStampTz("last_revised", x => x.last_revised)
                .MapBoolean("assume_complete", x => x.assume_complete)
                .MapInteger("download_status", x => x.download_status)
                .MapTimeStampTz("last_downloaded", x => x.last_downloaded)
                .MapVarchar("local_path", x => x.local_path);

    }

}
