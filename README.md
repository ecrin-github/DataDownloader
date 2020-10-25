# DataDownloader
Downloads data from mdr data sources to local files, stored as XML.

The functioning of the mdr begins with the creation of a local copy of all the source data. A folder is set up to receive the data, one per source, and the data dowenload process adds files to that folder. The local copy of the source data therefore grows gradually by accruing mnore and more data from the source, and at any time ithe folder holds all the relevant data from that source. Sources are trial registries and data repositories, and the mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files from the data obtained (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converting the data into XML files (e.g. for WHO ICTRP data).
The format of the XML files created vary from source to source but represent the initial stage in the process of converting the source data into a consistent schema.<br/><br/>
The program represents the first stage in the 4 stage MDR extraction process:<br/>
&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;**Download** => Harvest => Import => Aggregation<br/><br/>
For a much more detailed explanation of the extraction process,and the MDR system as a whole, please see the project wiki (landing page at https://ecrin-mdr.online/index.php/Project_Overview).<br/>

### Parameters
The system is currently a consiole app, and takes 3 parameters
* A 6 digit integer representing the source (e.g. 100120 is Clinical Trials,gov)
* A 2 digit intger representing the download type (see listing below). If not provided the default download type will be read from the database.
* A cut-off date for those download types that are date dependent. In such cases only files or pages that have been revised since the cutoff data provided will be downloaded.

The plan is to wrap a UI around the app at some point.


### Download Types
10:	Full initial fetch (download)<br/>
*Identifies and downloads XML files, one per record, from the entire data source.*

11:	Full initial fetch (scrape)	<br/>
*Scrapes data, creates XML files, one per record, from the entire data source.*

12:	Full update (download)<br/>	
*Identifies and downloads XML files, one per record, from new or revised records within the entire data source.*

13:	Full update (scrape)<br/>
*Scrapes data, creates XML files, one per record, from new or revised data within the entire data source.*

14:	Focused fetch (download)<br/>
*Identifies and downloads XML files, one per record, that meet the criteria of a focused search.*

15:	Focused fetch (scrape)<br/>
*Scrapes data, creates XML files, one per record, that meet the criteria of a focused search.*

16:	Focused update (download)<br/>
*Identifies and downloads XML files, one per record, from new or revised records that also meet the criteria of a focused search.*

17:	Focused update (scrape)<br/>
*Scrapes data, creates XML files, one per record, from new or revised data that also meet the criteria of a focused search.*

18:	Full search<br/>
*Identifies data or web page, including its URL, across the entire data source, for later examination and possible fetch*

19:	Focused search<br/>
*Identifies data or web page, including its URL, that meet the criteria of a focused search, for later examination and possible fetch*

20:	Full initial fetch (file)<br/>
*Downloads file (e.g. CSV file from WHO) as the precursor for processing. Converted to local XML files.*

21:	Full update (file)<br/>
*Downloads file with new and / or revised data (e.g. CSV file from WHO). Converted to local XML files.*


### Provenance
* Author: Steve Canham
* Organisation: ECRIN (https://ecrin.org)
* System: Clinical Research Metadata Repository (MDR)
* Project: EOSC Life
* Funding: EU H2020 programme, grant 824087

