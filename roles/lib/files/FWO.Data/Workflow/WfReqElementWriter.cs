﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data.Workflow
{
    public class WfReqElementWriter : WfElementBase
    {
        [JsonProperty("request_action"), JsonPropertyName("request_action")]
        public string RequestAction { get; set; } = Workflow.RequestAction.create.ToString();

        [JsonProperty("device_id"), JsonPropertyName("device_id")]
        public int? DeviceId { get; set; }

        public WfReqElementWriter()
        {}

        public WfReqElementWriter(WfReqElement element) : base(element)
        { 
            RequestAction = element.RequestAction;
            DeviceId = element.DeviceId;
            IpString = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null;
            IpEnd = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null;
        }
    }
}
