# DataDownloader
Downloads data from sources to local files, OR in some cases identifies files / pages for later downloading of the full data.<br/>
Sources are trial registries and data repositories.

#### *N.B. Sources being added gradually - Yoda and BioLincc at the moment, CTG, EUCTR, ISRCTN, WHO, PubMed to be added*

The mechanisms used include
* Downloading XML files directly from a source's API, (e.g. for ClicalTrials.gov, PubMed)
* Scraping web pages and generating the XML files (e.g. for ISRCTN, EUCTR, Yoda, BioLincc)
* Downloading CSV files and converrting the data into XML files.

The types of download are listed below. A particular source will have a default download type, dependent on the availability of an API, exposure of 'date last revised' etc. The default type can be overwritten by the input parameters.


The format of the XML files created vary from source to source but represent the initial stage in the process of converting the source data into a consistent schema.

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

