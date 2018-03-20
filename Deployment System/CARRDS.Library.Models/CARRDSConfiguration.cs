using System;
using System.Collections.Generic;
using System.Text;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using System.IO;

using Newtonsoft.Json;

namespace CARRDS.Library.Models
{
    public class CARRDSConfiguration
    {

        // The information about the build this came from
        public BuildInformation BuildInformation { get; set; } = new BuildInformation();

        // For now I will have a list of the parameters
        public List<Parameter> Parameters { get; set; } = new List<Parameter>();

        // This is my list of actions to take in response to changes in the stack state
        public List<StackStatusResponse> StackStatusResponses { get; set; } = new List<StackStatusResponse>();


        public void save(string filepath)
        {
            // Serializer settings are apparantly important. 
            var SerilaizerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects
            };

            File.WriteAllText(filepath, JsonConvert.SerializeObject(this, SerilaizerSettings));
        }

    }
}
