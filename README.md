# Graph Mail Relay
This is a simple SMTP server that relays emails via Graph API. Its designed as a work around for legacy applications for the upcoming deprecation of basic authentication for SMTP. It can be used as a drop in replacement for an existing SMTP server.

It supports unencrypted connections on port 25, SSL/TLS connections on port 465, and StartTLS connections on port 587. Authentication is optional and can be configured to require a username and password or to allow anonymous access.

Authentication when enabled is done via local AD first and then via Azure AD for accounts that are Azure only like shared mailboxes. If the Azure account has MFA enabled then authentication will fail as the flow used for password validation in Azure does not support MFA.

## Usage
1. Download the latest release from the releases page
2. Extract the contents of the zip file to a directory of your choice
3. Edit the `appsettings.json` file to configure the application (see below for details)
4. Run the application

## Running as a Service
If you wish to run the application as a service, you can use a tool like [NSSM (Non-Sucking Service Manager)](https://nssm.cc/) to create a Windows service for the application.

## Azure App Registration
To use this application, you need to register an application in Azure AD and grant it the necessary permissions to send emails via Graph API. Follow these steps to set up the app registration:
1. Go to the Azure portal and navigate to Azure Active Directory > App registrations > New registration
2. Enter a name for the application and select the appropriate supported account types (e.g., "Accounts in this organizational directory only")
3. Click "Register" to create the application
4. After the application is created, go to "Overview" and note down the "Application (client) ID" and "Directory (tenant) ID" as you will need them for configuration.
5. In the "Authentication" section, add a new redirect URI for a public client (mobile & desktop) with the pre-generated MSAL only option. The redirect URI should be in the format `msal{client-id}://auth`.
6. Next go to "API permissions" and add the `Mail.Send` permission under Microsoft Graph for the application (not delegated). Make sure to grant admin consent for the permission.
7. After the application is created, go to "Certificates & secrets" and create a new client secret. Note down the value of the client secret as you will need it for configuration.

## Configuration
All configuration is done via the `appsettings.json` file. The following parameters are available:
| Parameter | Description |
| --- | --- |
| `AuthenticationRequired` | Whether to require authentication for sending emails. If turned off authentication can still be passed but passwords are not validated. |
| `TenantId` | The tenant ID of the Azure AD application. |
| `ClientId` | The client ID of the Azure AD application. |
| `ClientSecret` | The client secret of the Azure AD application. |
| `CertificatePath` | The path to the certificate file (PFX) for SSL and StartTLS. |
| `CertificatePassword` | The password for the certificate file. |
| `AllowUnencrypted` | Whether to allow unencrypted connections (port 25). |
| `AllowSsl` | Whether to allow SSL/TLS connections (port 465). |
| `AllowStartTls` | Whether to allow STARTTLS connections (port 587). |
