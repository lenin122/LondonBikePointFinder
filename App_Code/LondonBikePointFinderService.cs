using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Web.Services;
using LondonBikePointFinderServiceApp.Models;
using System.Text;
using System.Web.Configuration;
using System.Web.Script.Services;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Summary description for LondonBikePointFinderService
/// </summary>
[WebService(Namespace = "http://lenin122testapp.somee.com/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
// To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
[ScriptService]
public class LondonBikePointFinderService : System.Web.Services.WebService
{
    private const string ConnectionStringName = "LONDONBASE";

    [ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
    [WebMethod(Description = "Test web method")]
    public string Test()
    {
        return "test";
    }

    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    [WebMethod(Description = "Refreshing top viewed BikePoints in radius search")]
    public string RefreshTopPoints(int topValue)
    {
        using (
            var sqlConnection =
                new SqlConnection(WebConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString))
        {
            {
                if (sqlConnection.State == System.Data.ConnectionState.Closed)
                    sqlConnection.Open();
                var sqlCommand = new SqlCommand(@"
                                            SELECT TOP (@top_value)
                                                [commonName],[counter] 
                                            FROM
                                                [LONDONBASE].[dbo].[BIKEPOINTS]
                                            ORDER BY counter desc", sqlConnection);
                sqlCommand.Parameters.Add("@top_value", SqlDbType.SmallInt).Value = topValue.ToString();
                var resultSB = new StringBuilder();

                resultSB.Append(@"<table id='top_table'>");
                using (var sqlDataReader = sqlCommand.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        resultSB.Append(String.Format(@"<tr><td>{0}</td></tr>",
                            sqlDataReader.GetString(0), sqlDataReader.GetInt32(1).ToString()));
                    }
                }

                resultSB.Append(@"</table>");
                return resultSB.ToString();
            }

        }
    }

    [ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
    [WebMethod(Description = "This method gets all nike points in london") ]
    public async Task<string> GetAllBikeStops()
    {

        try
        {
            List<BikePoints> bikePoints_List;
            HttpClient client = new HttpClient();

            HttpResponseMessage httpmessage = await client.GetAsync("https://api.tfl.gov.uk/BikePoint?app_id=57e5756c&app_key=fa0bd9e6338c902e6ef998ff24fd5607").ConfigureAwait(continueOnCapturedContext: false);
            httpmessage.EnsureSuccessStatusCode();
            string response = await httpmessage.Content.ReadAsStringAsync();
            //geted string
            bikePoints_List = await System.Threading.Tasks.Task.Factory.StartNew(() =>
                    JsonConvert.DeserializeObject<List<BikePoints>>(response));
            //make small copy in memory for jquery
            return await System.Threading.Tasks.Task.Factory.StartNew(() =>
                    JsonConvert.SerializeObject(bikePoints_List, Formatting.None)); //, Formatting.Indented));
        }
        catch (Exception ex)
        {
           return JsonConvert.SerializeObject(ex, Formatting.None);
        }

    }
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    [WebMethod(Description = "Returns json string with bike points in request radius around request lat/lng")]
    public async Task<string> GetPointsWithRadius(double lat, double lon, double radius)
    {
        
        StringBuilder answer = new StringBuilder();
        //getting json from webapi
        
        string url = String.Format("https://api.tfl.gov.uk/BikePoint?lat={0:0.####}&lon={1:1.####}&radius={2}&app_id=57e5756c&app_key=fa0bd9e6338c902e6ef998ff24fd5607",
            lat.ToString(CultureInfo.GetCultureInfo("en-US").NumberFormat), lon.ToString(CultureInfo.GetCultureInfo("en-US").NumberFormat),
            radius);

        var httpClient = new HttpClient();
        var httpmessage = await httpClient.GetAsync(url).ConfigureAwait(continueOnCapturedContext:false);
        httpmessage.EnsureSuccessStatusCode();
        answer.Append(await httpmessage.Content.ReadAsStringAsync());
        //get answer, and then  link list to answer.list
        var answerDeserialized = JsonConvert.DeserializeObject(answer.ToString(), typeof(PropertyAnswerModel));
        //count++ in sql for all points in bikePoints_list
        IncrementPoints(((PropertyAnswerModel)answerDeserialized).places);
        //return jsonstring to show this points
        answer.Clear();
        answer.Append(JsonConvert.SerializeObject(((PropertyAnswerModel)answerDeserialized).places, Formatting.None));
        return answer.ToString();
    }

    private void IncrementPoints(List<BikePoints> items)
    {
        SqlConnection connection = new SqlConnection(WebConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString);
        SqlTransaction transaction = null;
        bool everythingisgreat = true;
        try
        {

            if (connection.State == System.Data.ConnectionState.Closed)
                connection.Open();
            transaction = connection.BeginTransaction();
            foreach (BikePoints bp in items)
            {
                SqlCommand command = new SqlCommand(@"IF EXISTS(
                                                            SELECT commonName FROM [LONDONBASE].[dbo].[BIKEPOINTS] 
                                                            WHERE commonName = @commonName)
                                                            --value exists perform update
                                                            BEGIN
                                                                UPDATE [LONDONBASE].[dbo].[BIKEPOINTS]
					                                            SET [counter] = [counter] + 1
					                                            WHERE commonName = @commonName
                                                            END
                                                            ELSE
                                                            --value doesnt exist perform insert
                                                            BEGIN
                                                                INSERT INTO [LONDONBASE].[dbo].[BIKEPOINTS]
                                                                    ([commonName],[counter])
                                                                VALUES
                                                                    (@commonName, 1)
                                                            END", connection);
                command.Parameters.Add("@commonName", SqlDbType.VarChar).Value = bp.commonName;
                command.Transaction = transaction;
                everythingisgreat &= command.ExecuteNonQuery() == 1;
            }
            if (everythingisgreat)
                transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            transaction.Rollback();
        }
        finally
        {
            connection.Close();
        }
    }


    public LondonBikePointFinderService()
    {

        //Uncomment the following line if using designed components 
        //InitializeComponent(); 
    }

}
