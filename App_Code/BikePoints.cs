using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Runtime.Serialization;

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

    [DataContract]
    public class BikePoints
    {
        [DataMember]
        public string id { get; set; }
        public string url { get; set; }
        [DataMember]
        public string commonName { get; set; }
        public string placeType { get; set; }
        [DataMember]
        public List<AdditionalProperty> additionalProperties { private get; set; }
        public List<object> childrenUrls { get; set; }
        [DataMember]
        public double lat { get; set; }
        [DataMember]
        public double lon { get; set; }
        //[DataMember]
        public double freePointsPercent
        {
            get
            {
                return Double.Parse(this.additionalProperties[6].value) / Double.Parse(this.additionalProperties[8].value);
            }
        }

        public override string ToString()
        {
            return this.id + " " + this.commonName + " ";
        }

    }
    public class PropertyAnswerModel
    {
        public List<double> centrePoint { get; set; }
        [JsonProperty("places")]
        public List<BikePoints> places { get; set; }
    }
}