using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace LondonBikePointFinderServiceApp.Models
{
    public class AdditionalProperty
    {
        [JsonIgnore]
        public string category { get; set; }
        public string key { get; set; }
        [JsonIgnore]
        public string sourceSystemKey { get; set; }
        public string value { get; set; }
        [JsonIgnore]
        public string modified { get; set; }
    }
    
    public class BikePoint
    {
        [JsonProperty("id")]
        public string id { get; set; }
        [JsonIgnore]    
        public string url { get; set; }
        [JsonProperty("commonName")]
        public string commonName { get; set; }
        [JsonIgnore]
        public string placeType { get; set; }
        [JsonProperty("additionalProperties")]
        public List<AdditionalProperty> additionalProperties { private get; set; }
        [JsonIgnore]
        public List<object> childrenUrls { get; set; }
        [JsonProperty("lat")]
        public double lat { get; set; }
        [JsonProperty("lon")]
        public double lon { get; set; }
        [JsonIgnore]
        public double freeDocksPercent
        {
            get
            {
                return Double.Parse(this.additionalProperties[6].value) / Double.Parse(this.additionalProperties[8].value);
            }
        }
        [JsonIgnore]
        public int GetID
        {
            get
            {
                var regex = new Regex("\\d+");
                return Int32.Parse(regex.Match(this.id).Value);
            }
        }
        
        public override string ToString()
        {
            return this.id + " " + this.commonName + " ";
        }
        
        public BikePoint(int bp_id, string bp_commonName)
        {
            id = "BikePoints_" + bp_id;
            commonName = bp_commonName;
        }

    }
    public class PropertyAnswerModel
    {
        public List<double> centrePoint { get; set; }
        [JsonProperty("places")]
        public List<BikePoint> places { get; set; }
    }
}