# Azure-Databricks-CI-CD-Initial-Token
How to do CI/CD with Azure Databricks and get the initial Databricks token.

# Get the initial Databricks token via CI/CD pipeline in Azure

If you are automating your Databricks workspace creation in Azure you will probably run into an issue where you need a Databricks token to make REST calls to the Databricks API.  The ARM template should ideally return to you an initial token, but it does not.  You need to log into the UI and create a token which disrupts your CI/CD pipeline. So, I created the below workaround which still involves a person, but keeps your pipeline automated.

### Steps (High Level)
1. Create a Azure DevOps project 
2. Create a Resource group for a Key Vault and Azure Function
3. Seed the KeyVault with a secret (e.g. DatabricksInitialToken = "EMPTY") 
4. Create an Azure Function that reads the secret (this will be a CI/CD gate which will pause our pipeline)
5. In order to set the original very first token a person must login to Databricks and generate a token and then set the value in Key Vault (we might be able to automate this with Selenium... but logging into Azure AD can be tricky.  The ideal approach would be for the Databricks ARM template to optionally return a token)
5. Create a release pipeline that creates Databricks, checks key vault for the Databricks token, then interacts with the Databricks REST API

### Details
1. In Azure create a resource group named DatabricksInitialToken (I did East US).  
   If you do another region you need to update the CreateGroup.sh (the Databricks REST endpoint)
2. In Azure create a key vault named DatabricksInitialToken
   a. Open key vault and select Secrets
   b. Click Generate/Import
   c. Name it DatabricksInitialToken
   b. For the value enter EMPTY (this needs to match your Azure Function)
3. In Azure create a function app named DatabricksInitialToken
   a. Select to code in Portal
   b. Select HTTP/Webhook
   c. Click on DatabricksInitialToken | Platform features
      a) Select Managed Service Identity
      b) Enable this
   d. Now go back to your Key Vault
      a) Click on Access Control
      b) Click on Add
      c) Select Contributor (or lower)
      d) Enter DatabricksInitialToken for the name
      e) Select your Function App
   e. In your Key Vault 
      a) Click on Access Policies
      b) Select Secret Management (or lower, we just need to read a secret)
      c) For Service Principle search for DatabricksInitialToken and select
      d) Click okay then save
   f. Paste the AzureFunction.cs code into your function app (you might have to change some of the names)

4. Upload code to your repo (azuredeploy.json, azuredeploy.parameters.json, CreateGroup.sh)
5. Create a release pipeline
   a. Tie to your Git repo 
      (typically I do a build pipeline and publish artifacts, but we are "cheating" here)
   b. Create a Stage (empty job) named CreateDB
   c. Add the deploy ARM template task
      Get the ARM template for a Databricks workspace
      https://github.com/Azure/azure-quickstart-templates/tree/master/101-databricks-workspace
      a) Authorize your subscription (you might need to use a service principle under project | service connections )
      b) For resource group name enter: DatabricksInitialTokenCluster
      c) Select East US for location
      c) Select the template
      d) Select the parameters
      e) Override the template parameter with -workspaceName DatabricksInitialToken
   d. Save, Run and check (it should create a Databricks Azure Workspace!)
   e. Add a new Stage (empty job) named GetDGetDBTokenAndRunScriptBToken
      a) Add a gate (click the little lightning bold)
      b) Select your function app
      c) Get your function app URL and code (you get this at the top of your function app)
      d) Under Advanced select API response
      e) For Success Criteria enter: eq(root['status'], 'successful')
   f. Save, Run and check (it should fail since the key vault's secret is set to EMPTY)
   g. Open your Databricks workspace "MyClusterName"
      a) Click on user settings
      b) Generate a new token
   h. Open your key vault DatabricksInitialToken
      a) Update the secret value of DatabricksInitialToken with the token just generated
      b) You can run your Azure Function and it should return successful
   i) Edit your pipeline
      a) Click on Variables
      b) Click on Variables groups
      c) Link your Key Vault to a variable group by clicking Manage Variable Groups
      d) Under your GetDGetDBTokenAndRunScriptBToken stage 
      e) For Agent select Hosted Linux
      f) Select the script CreateGroup.sh
      g) For parameters enter $(DatabricksInitialToken)
    j. Save, Run and check (it should work)
      a) Run this command to check and delete the group created in Databricks so we can run again
        DatabricksToken=<<REPLACE TOKEN>
        
        curl -X GET  https://eastus.azuredatabricks.net/api/2.0/groups/list  \
        -H "Content-Type: application/json" \
        -H "Authorization: Bearer $DatabricksToken" 

        // To re-run delete the group
        curl -n \
        -H "Content-Type: application/json" \
        -H "Authorization: Bearer $DatabricksToken" \
        -X POST -d @- https://eastus.azuredatabricks.net/api/2.0/groups/delete <<JSON
        {
            "group_name": "VSTSGroup"
        }
        JSON
      b) Go to your Key Vault and change the secret to EMPTY
      c) Run your pipeline
      b) The gate should fail (I set my retry to 5 minutes, so I have to wait 5 minutes)
      c) While waiting go update your Key Vault and set the secret to your Databricks token
      d) The gate should pass
      e) The script should run and create the VSTS group (you can check with the above curl script, if you not have curl installed go to shell.azure.com and use the Bash prompt)

### Improvements
1. Make Azure Function read the secret name from the POST body so you can for many different workspaces
2. See my script for rotating Databricks token and combine this method with the rotation technique!
   https://github.com/AdamPaternostro/Azure-Databricks-Token-Rotation
