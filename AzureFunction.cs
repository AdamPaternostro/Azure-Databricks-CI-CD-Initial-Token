#r "Newtonsoft.Json"
using System.Net;
using System.Net.Http.Formatting;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, ILogger log)
{
  log.LogInformation("C# HTTP trigger function processed a request.");

  string apiversion = "2017-09-01";
  string resource = "https://vault.azure.net";
  string msiEndpoint = Environment.GetEnvironmentVariable("MSI_ENDPOINT");
  string endpoint = $"{msiEndpoint}/?resource={resource}&api-version={apiversion}";
  string msiSecret = Environment.GetEnvironmentVariable("MSI_SECRET");
  System.Net.Http.HttpClient tokenClient = new System.Net.Http.HttpClient();
  tokenClient.DefaultRequestHeaders.Add("Secret", msiSecret);
  JObject tokenServiceResponse = JsonConvert.DeserializeObject<JObject>(await tokenClient.GetStringAsync(endpoint));
  string token = tokenServiceResponse["access_token"].ToString();
  
  apiversion="2016-10-01";
  string secretName = "DatabricksInitialToken";
  string keyVaultURL = "https://databricksinitialtoken.vault.azure.net/";
  endpoint = $"{keyVaultURL}secrets/{secretName}/?api-version={apiversion}";
  System.Net.Http.HttpClient keyVaultClient = new System.Net.Http.HttpClient();
  keyVaultClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
  JObject keyVaulteResponse = JsonConvert.DeserializeObject<JObject>(await keyVaultClient.GetStringAsync(endpoint));
  string keyVaultSecretValue = keyVaulteResponse["value"].ToString();

  // Do this only for debugging
  // log.LogInformation("keyVaultSecretValue: " + keyVaultSecretValue);

  string status = "failed";
  if (keyVaultSecretValue.ToUpper() != "EMPTY")
  {
     status = "successful";
  }
 
  // Do not return the secret VSTS will get it
  // var returnValue = new { status = status, secret = keyVaultSecretValue};
  var returnValue = new { status = status };
  var jsonToReturn = JsonConvert.SerializeObject(returnValue);

  return new HttpResponseMessage(HttpStatusCode.OK) 
    {
        Content = new StringContent(jsonToReturn, System.Text.Encoding.UTF8, "application/json")
    };

}
