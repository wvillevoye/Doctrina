# Voegt Microsoft Entra ID toe als identity provider ("Inloggen met Microsoft") aan een realm.
# Leest ENTRA_TENANT_ID, ENTRA_CLIENT_ID, ENTRA_CLIENT_SECRET uit .env. Kan herhaald worden (maakt aan of werkt bij).
# Gebruik:  powershell -ExecutionPolicy Bypass -File scripts\configure-entra.ps1 [-Realm doctrina|demo]
param([ValidateSet("doctrina","demo")][string]$Realm = "doctrina")
$ErrorActionPreference = "Stop"
$root = Join-Path $PSScriptRoot ".."
$envFile = Join-Path $root ".env"
if (-not (Test-Path $envFile)) { throw "Geen .env gevonden. Kopieer .env.example naar .env en vul de Entra-gegevens in." }

$cfg = @{}
Get-Content $envFile | Where-Object { $_ -match '^\s*([A-Z_]+)\s*=\s*(.*)\s*$' } | ForEach-Object { $cfg[$Matches[1]] = $Matches[2].Trim('"') }
foreach ($k in "ENTRA_TENANT_ID","ENTRA_CLIENT_ID","ENTRA_CLIENT_SECRET") {
    if (-not $cfg[$k] -or $cfg[$k] -like "<*") { throw "$k ontbreekt in .env" }
}
$t = $cfg.ENTRA_TENANT_ID
$adminUser = if ($cfg.KC_ADMIN_USER) { $cfg.KC_ADMIN_USER } else { "admin" }
$adminPass = if ($cfg.KC_ADMIN_PASSWORD) { $cfg.KC_ADMIN_PASSWORD } else { "admin" }

function Kc { docker exec keycloak /opt/keycloak/bin/kcadm.sh @args; if ($LASTEXITCODE -ne 0) { throw "kcadm faalde: $args" } }
function Copy-Json($obj, $name) {
    $f = Join-Path $env:TEMP $name
    [IO.File]::WriteAllText($f, (ConvertTo-Json $obj -Depth 5), (New-Object Text.UTF8Encoding $false))
    docker cp $f "keycloak:/tmp/$name" | Out-Null
}

Kc config credentials --server http://localhost:8080 --realm master --user $adminUser --password $adminPass | Out-Null

$idp = [ordered]@{
    alias = "entra"; providerId = "oidc"; displayName = "Inloggen met Microsoft (Entra ID)"
    enabled = $true; trustEmail = $true; storeToken = $false; firstBrokerLoginFlowAlias = "first broker login"
    config = [ordered]@{
        issuer            = "https://login.microsoftonline.com/$t/v2.0"
        authorizationUrl  = "https://login.microsoftonline.com/$t/oauth2/v2.0/authorize"
        tokenUrl          = "https://login.microsoftonline.com/$t/oauth2/v2.0/token"
        logoutUrl         = "https://login.microsoftonline.com/$t/oauth2/v2.0/logout"
        jwksUrl           = "https://login.microsoftonline.com/$t/discovery/v2.0/keys"
        useJwksUrl        = "true"; validateSignature = "true"
        clientId          = $cfg.ENTRA_CLIENT_ID
        clientSecret      = $cfg.ENTRA_CLIENT_SECRET
        clientAuthMethod  = "client_secret_post"
        defaultScope      = "openid profile email"
        pkceEnabled       = "true"; pkceMethod = "S256"
        syncMode          = "FORCE"
        guiOrder          = "1"
    }
}
Copy-Json $idp "entra-idp.json"

$ErrorActionPreference = "Continue"
$null = cmd /c "docker exec keycloak /opt/keycloak/bin/kcadm.sh get identity-provider/instances/entra -r $Realm >nul 2>nul"
$exists = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = "Stop"
if ($exists) {
    Kc update identity-provider/instances/entra -r $Realm -f /tmp/entra-idp.json
    Write-Host "Entra identity provider bijgewerkt in realm '$Realm'."
} else {
    Kc create identity-provider/instances -r $Realm -f /tmp/entra-idp.json
    Write-Host "Entra identity provider aangemaakt in realm '$Realm'."
}

$existing = (docker exec keycloak /opt/keycloak/bin/kcadm.sh get identity-provider/instances/entra/mappers -r $Realm --fields name --format csv --noquotes) -split "`n" | ForEach-Object { $_.Trim() }
function AddMapper($name, $type, [hashtable]$conf) {
    if ($existing -contains $name) { return }
    Copy-Json ([ordered]@{ name = $name; identityProviderAlias = "entra"; identityProviderMapper = $type; config = $conf }) "entra-mapper.json"
    Kc create identity-provider/instances/entra/mappers -r $Realm -f /tmp/entra-mapper.json | Out-Null
    Write-Host "  mapper: $name"
}

# Entra app-rollen (claim 'roles') -> Keycloak realm-rollen
$roleMap = if ($Realm -eq "doctrina") { @{ "admin" = "admin"; "employee" = "employee"; "student" = "student" } }
           else                       { @{ "Medewerker" = "medewerker"; "Student" = "student" } }
foreach ($entraRole in $roleMap.Keys) {
    AddMapper "rol-$($roleMap[$entraRole])" "oidc-role-idp-mapper" @{ syncMode = "FORCE"; claim = "roles"; "claim.value" = $entraRole; role = $roleMap[$entraRole] }
}
# Afdeling (optionele claim) en Entra object-id bewaren als attribuut
AddMapper "attr-department"     "oidc-user-attribute-idp-mapper" @{ syncMode = "FORCE"; claim = "department"; "user.attribute" = "department" }
AddMapper "attr-entraObjectId"  "oidc-user-attribute-idp-mapper" @{ syncMode = "FORCE"; claim = "oid";        "user.attribute" = "entraObjectId" }

Write-Host ""
Write-Host "Entra app-rollen (value) die herkend worden: $($roleMap.Keys -join ', ')"
Write-Host "Redirect URI voor de Entra app-registratie (platform Web):"
Write-Host "  http://localhost:8080/realms/$Realm/broker/entra/endpoint"
