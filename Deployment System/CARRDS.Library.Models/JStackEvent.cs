using System;
using System.Collections.Generic;
using System.Text;

namespace CARRDS.Library.Models
{
    public class JStackEvent : Amazon.CloudFormation.Model.StackEvent
    {

        public string SESMessage;

        public JStackEvent(string SESMessage)
        {
            this.SESMessage = SESMessage;

            ClientRequestToken = FindValue("ClientRequestToken");
            EventId = FindValue("EventId");
            LogicalResourceId = FindValue("LogicalResourceId");
            PhysicalResourceId = FindValue("PhysicalResourceId");
            ResourceProperties = FindValue("ResourceProperties");
            ResourceStatus = FindValue("ResourceStatus");
            ResourceStatusReason = FindValue("ResourceStatusReason");
            ResourceType = FindValue("ResourceType");
            StackId = FindValue("StackId");
            StackName = FindValue("StackName");
            Timestamp = Convert.ToDateTime(FindValue("Timestamp"));

        }


        private string FindValue(string member)
        {
            try
            {
                int ind = SESMessage.IndexOf(member);
                int indstart = SESMessage.IndexOf('\'', ind) + 1;
                int indend = SESMessage.IndexOf('\'', indstart);
                return SESMessage.Substring(indstart, indend - indstart);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

    }
}
