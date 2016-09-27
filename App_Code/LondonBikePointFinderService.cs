using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
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
[ScriptService]
public class LondonBikePointFinderService : WebService
{

    protected static string BaseUrl = @"https://api.tfl.gov.uk/";
    // ReSharper disable once InconsistentNaming
    protected static string URL_AllBikePoints = BaseUrl + @"BikePoint?app_id={0}&app_key={1}";
    // ReSharper disable once InconsistentNaming
    protected static string URL_BikePointsByRadius = URL_AllBikePoints + @"&lat={2:2.####}&lon={3:3.####}&radius={4}";
    protected static NumberFormatInfo DefaultNumberFormat = CultureInfo.GetCultureInfo("en-US").NumberFormat;

    private const string ConnectionName = "LONDONBASE";
    protected string ConnectionString { get; private set; }

    protected string AppId { get; private set; }
    protected string AppKey { get; private set; }

    // ReSharper disable once InconsistentNaming
    protected string SetRequestInfo(string URLString, params string[] requestParams)
    {
        // ReSharper disable once CoVariantArrayConversion
        return String.Format(URLString, requestParams);
    }

    [ScriptMethod(ResponseFormat = ResponseFormat.Json, UseHttpGet = true)]
    [WebMethod(Description = "This method gets all nike points in london")]
    public async Task<string> GetAllBikeStops()
    {
        try
        {
            var client = new HttpClient();
            var requestUrl = SetRequestInfo(URL_AllBikePoints, AppId, AppKey);
            var httpmessage = await client.GetAsync(requestUrl).ConfigureAwait(continueOnCapturedContext: false);
            httpmessage.EnsureSuccessStatusCode();

            return await httpmessage.Content.ReadAsStringAsync();
            //we can reduce size with serialize/desirialize, while cutting not used properties to ~0.4*size
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(ex, Formatting.None);
        }
    }


    [WebMethod(Description = "Returns json string with bike points in request radius around request lat/lng")]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public async Task<string> GetPointsByRadius(double lat, double lon, double radius)
    {
        var requestUrL = SetRequestInfo(URL_BikePointsByRadius, AppId, AppKey,
            lat.ToString(DefaultNumberFormat), lon.ToString(DefaultNumberFormat), radius.ToString(DefaultNumberFormat));
        var httpClient = new HttpClient();
        var httpmessage = await httpClient.GetAsync(requestUrL).ConfigureAwait(continueOnCapturedContext: false);
        httpmessage.EnsureSuccessStatusCode();

        var answer = new StringBuilder();
        answer.Append(await httpmessage.Content.ReadAsStringAsync());
        //get answer, and then  link list to answer.list
        var answerDeserialized = JsonConvert.DeserializeObject(answer.ToString(), typeof(PropertyAnswerModel));
        //count++ in sql for all points in bikePoints_list
        IncrementStatistics(((PropertyAnswerModel) answerDeserialized).places);
        //return jsonstring to show this points
        answer.Clear();
        answer.Append(JsonConvert.SerializeObject(((PropertyAnswerModel) answerDeserialized).places, Formatting.None));
        return answer.ToString();
    }

    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    [WebMethod(Description = "Refreshing top viewed BikePoint in radius search")]
    public string GetTopPoints(int topValue)
    {
        try
        {
            var answer = new StringBuilder("[");
            foreach (BikePoint bp in SQLRequestTopBikePoints(topValue))
            {
                answer.Append("{\"commonName\":\"");
                answer.Append(bp.commonName);
                answer.Append("\"},");
            }
            answer[answer.Length - 1] = ']';
            return answer.ToString();
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(ex, Formatting.Indented);
        }
        
        //[{"commonName":"Abbey Orchard Street, Westminster"},{"commonName":"asdasd"}, {"commonName":"asdas"}]
    }

    // ReSharper disable once InconsistentNaming
    protected List<BikePoint> SQLRequestTopBikePoints(int topValue)
    {
        var resultList = new List<BikePoint>();
        using (SqlConnection connection = new SqlConnection(ConnectionString))
        {
            SqlCommand command = new SqlCommand(@"
                                            SELECT TOP (@top_value)
                                                [BikePoint_id],[commonName],[counter] 
                                            FROM
                                                [LONDONBASE].[dbo].[BIKEPOINTS]
                                            ORDER BY counter desc", connection);
            command.Parameters.Add("@top_value", SqlDbType.SmallInt).Value = topValue.ToString();
            command.Connection.Open();
            using (var sqlDataReader = command.ExecuteReader())
            {
                while (sqlDataReader.Read())
                {
                    resultList.Add(new BikePoint(sqlDataReader.GetInt32(0), sqlDataReader.GetString(1)));
                    //TODO: get actual commonName by id from api 
                }
            }
        }
        return resultList;
    }

    private void IncrementStatistics(List<BikePoint> items)
    {
        using (SqlConnection connection = new SqlConnection(ConnectionString))
        {
            SqlTransaction transaction = null;
            bool everythingisgreat = true;
            try
            {
                if (connection.State == ConnectionState.Closed)
                    connection.Open();
                transaction = connection.BeginTransaction();
                foreach (BikePoint bp in items)
                {
                    SqlCommand command = new SqlCommand(@"IF EXISTS(
                                                            SELECT commonName FROM [LONDONBASE].[dbo].[BIKEPOINTS] 
                                                            WHERE BikePoint_id = @BikePoint_id AND commonName = @commonName)
                                                            --value exists perform update
                                                            BEGIN
                                                                UPDATE [LONDONBASE].[dbo].[BIKEPOINTS]
					                                            SET [counter] = [counter] + 1
					                                            WHERE BikePoint_id = @BikePoint_id AND commonName = @commonName
                                                            END
                                                            ELSE
                                                            --value doesnt exist perform insert
                                                            BEGIN
                                                                INSERT INTO [LONDONBASE].[dbo].[BIKEPOINTS]
                                                                    ([BikePoint_id],[commonName],[counter])
                                                                VALUES
                                                                    (@BikePoint_id,@commonName, 1)
                                                            END", connection);
                    //
                    command.Parameters.Add("@commonName", SqlDbType.VarChar).Value = bp.commonName;
                    command.Parameters.Add("@BikePoint_id", SqlDbType.Int).Value = bp.GetID;
                    //TODO: get actual commonName by id from api 
                    command.Transaction = transaction;
                    everythingisgreat &= command.ExecuteNonQuery() == 1;
                }
                if (everythingisgreat)
                    transaction.Commit();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (transaction != null)
                {
                    transaction.Rollback();
                }
            }
        }
    }



    public LondonBikePointFinderService()
    {
        AppId = "57e5756c";
        AppKey = "fa0bd9e6338c902e6ef998ff24fd5607";
        ConnectionString = WebConfigurationManager.ConnectionStrings[ConnectionName].ConnectionString;
    }

}
