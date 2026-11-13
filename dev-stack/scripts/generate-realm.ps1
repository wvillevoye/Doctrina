# Genereert realm-bestanden met vaste testaccounts + N nepgebruikers (medewerkers/studenten).
#   realm/demo-realm.json      algemene speeltuin (clients demo-app / demo-api)
#   realm/doctrina-realm.json  voor Doctrina (clients doctrina-web / doctrina-api / doctrina-sis-sync)
# Gebruik:  powershell -ExecutionPolicy Bypass -File scripts\generate-realm.ps1 [-Realm all|demo|doctrina] [-Count 100] [-StudentRatio 0.7] [-Seed 42]
# Daarna:   docker compose down -v; docker compose up -d
param(
    [ValidateSet("all","demo","doctrina")][string]$Realm = "all",
    [int]$Count = 250,
    [double]$StudentRatio = 0.7,
    [int]$Seed = 42,
    [string]$Password = "test123",
    [string]$Domain = "example.test"
)
$ErrorActionPreference = "Stop"

$firstNames = @("Daan","Sem","Lucas","Levi","Finn","Noah","Milan","Luuk","Bram","Thijs","Jesse","Ruben","Lars","Tim","Sven","Jasper","Max","Thomas","Stijn","Koen",
                "Emma","Julia","Mila","Sophie","Tess","Zoe","Sara","Lotte","Eva","Anna","Noor","Fleur","Lisa","Iris","Femke","Sanne","Lieke","Roos","Maud","Esmee",
                "Yusuf","Mohamed","Ayoub","Fatima","Amira","Priya","Chen","Lena","Jonas","Elif")
$lastNames  = @("de Jong","Jansen","de Vries","van den Berg","van Dijk","Bakker","Janssen","Visser","Smit","Meijer","de Boer","Mulder","de Groot","Bos","Vos",
                "Peters","Hendriks","van Leeuwen","Dekker","Brouwer","de Wit","Dijkstra","Smits","de Graaf","van der Meer","Kok","Jacobs","Vermeulen","Willems","Hermans",
                "Lemmens","Claessens","Schreurs","Coenen","Wouters","Huntjens","Ramaekers","Eurlings","Vroemen","Kuipers")
# Indeling Universiteit Maastricht: 6 faculteiten + service centres (department = korte code, departmentName = volledige naam)
$faculties = @(
    @{ code = "FHML";  name = "Faculty of Health, Medicine and Life Sciences"; programs = @("Geneeskunde","Biomedical Sciences","Health Sciences") },
    @{ code = "SBE";   name = "School of Business and Economics";             programs = @("International Business","Econometrics and Operations Research","Economics and Business Economics") },
    @{ code = "FdR";   name = "Faculteit der Rechtsgeleerdheid";              programs = @("Nederlands recht","European Law School") },
    @{ code = "FPN";   name = "Faculty of Psychology and Neuroscience";       programs = @("Psychology","Cognitive and Clinical Neuroscience") },
    @{ code = "FASoS"; name = "Faculty of Arts and Social Sciences";          programs = @("Arts and Culture","European Studies") },
    @{ code = "FSE";   name = "Faculty of Science and Engineering";           programs = @("Data Science and Artificial Intelligence","Circular Engineering","Maastricht Science Programme") }
)
$services = @(
    @{ code = "ICTS"; name = "ICT Service Centre";          jobs = @("ICT-medewerker","Applicatiebeheerder","Functioneel beheerder","Servicedeskmedewerker") },
    @{ code = "FS";   name = "Facility Services";           jobs = @("Facilitair medewerker","Inkoper","Beveiliger","Huisvestingsadviseur") },
    @{ code = "SSC";  name = "Student Services Centre";     jobs = @("Studentadviseur","Medewerker inschrijving","Studentendecaan") },
    @{ code = "UB";   name = "University Library";          jobs = @("Informatiespecialist","Medewerker collectie") },
    @{ code = "FIN";  name = "Finance";                     jobs = @("Financieel medewerker","Controller") },
    @{ code = "MUO";  name = "Maastricht University Office"; jobs = @("Beleidsmedewerker","Juridisch adviseur","Communicatieadviseur","HR-adviseur") }
)
$academicJobs = @("Docent","Universitair docent","Universitair hoofddocent","Hoogleraar","Promovendus","Onderzoeker","Onderwijscoördinator","Secretaresse")
$serviceShare = 0.3   # deel van de medewerkers dat bij een dienst werkt i.p.v. een faculteit

function To-Slug([string]$s) { ($s.ToLower() -replace "[^a-z0-9]", "") }

# ---------------------------------------------------------------- gebruikers
# $aff: hoe "medewerker"/"student" heet in deze realm (rol + attribuutwaarde + groep)
#       en optioneel $aff.numbers: nummerformaat + personId (realm doctrina)
function New-Users($fixed, $aff) {
    $null = Get-Random -SetSeed $Seed
    $users = New-Object System.Collections.ArrayList
    $usedNumbers = @{}
    # Precies één Get-Random per gebruiker, zodat namen gelijk blijven bij een ander nummerformaat.
    function Next-Number([bool]$student) {
        $s = $aff.numbers
        if (-not $s) {   # oud formaat (realm demo): i6xxxxxx / Exxxxx
            if ($student) { return "i6" + (Get-Random -Minimum 100000 -Maximum 999999) }
            return "E" + (Get-Random -Minimum 10000 -Maximum 99999)
        }
        $prefix = if ($student) { $s.studentPrefix } else { $s.employeePrefix }
        $n = Get-Random -Minimum 0 -Maximum 1000000
        while ($usedNumbers.ContainsKey("$prefix$($n.ToString('D6'))")) { $n = ($n + 1) % 1000000 }
        $nr = "$prefix$($n.ToString('D6'))"; $usedNumbers[$nr] = $true
        return $nr
    }
    foreach ($x in $fixed) {
        $attrs = [ordered]@{ affiliation = @($x.aff); department = @($x.dep); departmentName = @((@($faculties) + @($services) | Where-Object { $_.code -eq $x.dep }).name) }
        if ($x.nr) {
            $usedNumbers[$x.nr] = $true
            if ($x.aff -eq $aff.student.value) { $attrs.studentNumber = @($x.nr) } else { $attrs.employeeNumber = @($x.nr) }
            if ($aff.numbers.personId) { $attrs.personId = @($x.nr) }
        }
        [void]$users.Add([ordered]@{
            username = $x.u; email = "$($x.u)@$Domain"; firstName = $x.f; lastName = $x.l
            enabled = $true; emailVerified = $true
            attributes = $attrs
            credentials = @(@{ type = "password"; value = $x.u; temporary = $false })
            realmRoles = $x.roles; groups = $x.groups
        })
    }
    $used = @{}
    $studentCount = [math]::Round($Count * $StudentRatio)
    for ($i = 1; $i -le $Count; $i++) {
        $isStudent = $i -le $studentCount
        $fn = $firstNames | Get-Random
        $ln = $lastNames  | Get-Random
        $base = "$(To-Slug $fn).$(To-Slug ($ln -replace '^(van der|van den|van|de) ', ''))"
        $uname = $base; $n = 2
        while ($used.ContainsKey($uname)) { $uname = "$base$n"; $n++ }
        $used[$uname] = $true
        $fac = $faculties | Get-Random
        $attrs = [ordered]@{}
        if ($isStudent) {
            $attrs.department     = @($fac.code)
            $attrs.departmentName = @($fac.name)
            $attrs.affiliation    = @($aff.student.value)
            $attrs.studentNumber  = @(Next-Number $true)
            $attrs.program        = @($fac.programs | Get-Random)
            $email = "$uname@student.$Domain"; $a = $aff.student
        } else {
            if ((Get-Random -Maximum 1.0) -lt $serviceShare) { $unit = $services | Get-Random; $job = $unit.jobs | Get-Random }
            else                                             { $unit = $fac;                   $job = $academicJobs | Get-Random }
            $attrs.department     = @($unit.code)
            $attrs.departmentName = @($unit.name)
            $attrs.affiliation    = @($aff.employee.value)
            $attrs.employeeNumber = @(Next-Number $false)
            $attrs.jobTitle       = @($job)
            $email = "$uname@$Domain"; $a = $aff.employee
        }
        if ($aff.numbers.personId) { $attrs.personId = @(@($attrs.studentNumber) + @($attrs.employeeNumber) | Where-Object { $_ } | Select-Object -First 1) }
        [void]$users.Add([ordered]@{
            username = $uname; email = $email; firstName = $fn; lastName = $ln
            enabled = $true; emailVerified = $true; attributes = $attrs
            credentials = @(@{ type = "password"; value = $Password; temporary = $false })
            realmRoles = @($a.role); groups = @("/$($a.group)")
        })
    }
    return ,$users
}

# ---------------------------------------------------------------- user profile (Keycloak 24+)
function New-UserProfile($affValues) {
    $adminOnlyEdit = @{ view = @("admin","user"); edit = @("admin") }
    $userEdit      = @{ view = @("admin","user"); edit = @("admin","user") }
    function Attr($name, $display, $extra = @{}) {
        $a = [ordered]@{ name = $name; displayName = $display; permissions = $adminOnlyEdit; multivalued = $false; group = "organisatie" }
        foreach ($k in $extra.Keys) { $a[$k] = $extra[$k] }; $a
    }
    $cfg = [ordered]@{
        unmanagedAttributePolicy = "ADMIN_EDIT"
        attributes = @(
            [ordered]@{ name = "username"; displayName = '${username}'; permissions = $userEdit; multivalued = $false
                        validations = [ordered]@{ length = @{ min = 3; max = 255 }; "username-prohibited-characters" = @{}; "up-username-not-idn-homograph" = @{} } },
            [ordered]@{ name = "email"; displayName = '${email}'; required = @{ roles = @("user") }; permissions = $userEdit; multivalued = $false
                        validations = [ordered]@{ email = @{}; length = @{ max = 255 } } },
            [ordered]@{ name = "firstName"; displayName = '${firstName}'; required = @{ roles = @("user") }; permissions = $userEdit; multivalued = $false
                        validations = [ordered]@{ length = @{ max = 255 }; "person-name-prohibited-characters" = @{} } },
            [ordered]@{ name = "lastName"; displayName = '${lastName}'; required = @{ roles = @("user") }; permissions = $userEdit; multivalued = $false
                        validations = [ordered]@{ length = @{ max = 255 }; "person-name-prohibited-characters" = @{} } },
            (Attr "affiliation" "Aanstelling" @{ validations = @{ options = @{ options = $affValues } }; annotations = @{ inputType = "select" } }),
            # Vrij tekstveld (geen keuzelijst): via Entra kunnen andere waarden binnenkomen
            (Attr "department" "Faculteit / dienst (code, bv. FASoS, ICTS)"),
            (Attr "departmentName" "Faculteit / dienst (naam)"),
            (Attr "employeeNumber" "Personeelsnummer"),
            (Attr "jobTitle" "Functie"),
            (Attr "studentNumber" "Studentnummer"),
            (Attr "program" "Opleiding"),
            (Attr "personId" "Persoon-id (I6.../P7..., OOAPI personId)"),
            (Attr "entraObjectId" "Entra object-id")
        )
        groups = @(
            [ordered]@{ name = "user-metadata"; displayHeader = "User metadata"; displayDescription = "Attributes, which refer to user metadata" },
            [ordered]@{ name = "organisatie"; displayHeader = "Organisatie"; displayDescription = "Aanstelling en organisatiegegevens" }
        )
    }
    @{ "org.keycloak.userprofile.UserProfileProvider" = @(
        @{ providerId = "declarative-user-profile"; subComponents = @{}; config = @{ "kc.user.profile.config" = @((ConvertTo-Json -InputObject $cfg -Depth 20 -Compress)) } }
    ) }
}

# ---------------------------------------------------------------- protocol mappers
function M-Attr($attr, $claim) {
    [ordered]@{ name = $claim; protocol = "openid-connect"; protocolMapper = "oidc-usermodel-attribute-mapper"; consentRequired = $false
        config = [ordered]@{ "user.attribute" = $attr; "claim.name" = $claim; "jsonType.label" = "String"
                             "id.token.claim" = "true"; "access.token.claim" = "true"; "userinfo.token.claim" = "true"; "introspection.token.claim" = "true" } }
}
function M-Groups { [ordered]@{ name = "groups"; protocol = "openid-connect"; protocolMapper = "oidc-group-membership-mapper"; consentRequired = $false
        config = [ordered]@{ "claim.name" = "groups"; "full.path" = "false"; "id.token.claim" = "true"; "access.token.claim" = "true"; "userinfo.token.claim" = "true" } } }
# Platte "roles"-claim met realm-rollen, zoals Entra app-rollen
function M-RealmRolesFlat { [ordered]@{ name = "roles (Entra-stijl)"; protocol = "openid-connect"; protocolMapper = "oidc-usermodel-realm-role-mapper"; consentRequired = $false
        config = [ordered]@{ "claim.name" = "roles"; "multivalued" = "true"; "jsonType.label" = "String"
                             "id.token.claim" = "true"; "access.token.claim" = "true"; "userinfo.token.claim" = "true"; "introspection.token.claim" = "true" } } }
# Platte "roles"-claim met client-rollen van een andere client (Entra: application permissions bij client credentials)
function M-ClientRolesFlat($clientId) { [ordered]@{ name = "roles ($clientId)"; protocol = "openid-connect"; protocolMapper = "oidc-usermodel-client-role-mapper"; consentRequired = $false
        config = [ordered]@{ "usermodel.clientRoleMapping.clientId" = $clientId; "claim.name" = "roles"; "multivalued" = "true"; "jsonType.label" = "String"
                             "access.token.claim" = "true"; "introspection.token.claim" = "true"; "id.token.claim" = "false"; "userinfo.token.claim" = "false" } } }
function M-Audience($aud) { [ordered]@{ name = "aud $aud"; protocol = "openid-connect"; protocolMapper = "oidc-audience-mapper"; consentRequired = $false
        config = [ordered]@{ "included.client.audience" = $aud; "access.token.claim" = "true"; "id.token.claim" = "false"; "introspection.token.claim" = "true" } } }
# "oid" = Keycloak user-id (Entra: object-id van de gebruiker)
function M-Oid { [ordered]@{ name = "oid"; protocol = "openid-connect"; protocolMapper = "oidc-usermodel-property-mapper"; consentRequired = $false
        config = [ordered]@{ "user.attribute" = "id"; "claim.name" = "oid"; "jsonType.label" = "String"
                             "id.token.claim" = "true"; "access.token.claim" = "true"; "userinfo.token.claim" = "true"; "introspection.token.claim" = "true" } } }
$userMappers = @(
    (M-Attr "affiliation" "affiliation"), (M-Attr "department" "department"), (M-Attr "departmentName" "department_name"),
    (M-Attr "employeeNumber" "employee_number"), (M-Attr "studentNumber" "student_number"), (M-Groups)
)

function Write-Realm($obj, $file) {
    $out = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\realm\$file"))
    [IO.File]::WriteAllText($out, (ConvertTo-Json -InputObject $obj -Depth 30), (New-Object Text.UTF8Encoding $false))
    Write-Host "Geschreven: $out  ($($obj.users.Count) gebruikers)"
}

# ---------------------------------------------------------------- Doctrina CSV files (change add-academic-core, design D9)
# The same people as in realm doctrina (same personId), plus organizations, sessions, courses, offerings and
# enrolments, so the test accounts see courses under "My courses" after importing. Own random seed: the realm JSON
# does not change because of this. Import order: organizations, academic-sessions, courses, course-offerings, persons, associations.
$courses = @(
    @{ id = "MED1001"; name = "Anatomy and physiology";             fac = "FHML";  ects = 6 },
    @{ id = "BMS2002"; name = "Molecular biology";                  fac = "FHML";  ects = 6 },
    @{ id = "EBC1010"; name = "Microeconomics";                     fac = "SBE";   ects = 6.5 },
    @{ id = "EBC2020"; name = "Statistics for business";            fac = "SBE";   ects = 6.5 },
    @{ id = "LAW1003"; name = "European constitutional law";        fac = "FdR";   ects = 6 },
    @{ id = "PSY1001"; name = "Introduction to psychology";         fac = "FPN";   ects = 6 },
    @{ id = "PSY2004"; name = "Cognitive neuroscience";             fac = "FPN";   ects = 6 },
    @{ id = "ESS1001"; name = "History of European integration";    fac = "FASoS"; ects = 5 },
    @{ id = "KEN1110"; name = "Introduction to computer science";   fac = "FSE";   ects = 6 },
    @{ id = "KEN1130"; name = "Discrete mathematics";               fac = "FSE";   ects = 6 },
    @{ id = "SCI1004"; name = "Cell biology";                       fac = "FSE";   ects = 5 }
)

function Csv-Value($v) {
    $s = "$v"
    if ($s -match '[",\r\n]') { return '"' + ($s -replace '"', '""') + '"' }
    return $s
}

function Write-Csv($file, [string[]]$columns, $rows) {
    $dir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\data"))
    [void][IO.Directory]::CreateDirectory($dir)
    $lines = @($columns -join ",") + @($rows | ForEach-Object { $r = $_; ($columns | ForEach-Object { Csv-Value $r[$_] }) -join "," })
    [IO.File]::WriteAllText((Join-Path $dir $file), (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
    Write-Host "Geschreven: $(Join-Path $dir $file)  ($(@($rows).Count) regels)"
}

function Write-AcademicCsv($people) {
    $null = Get-Random -SetSeed ($Seed + 1)
    Write-Csv "organizations.csv" @("organizationId","name","shortName","organizationType","parentOrganizationId") (
        @(@{ organizationId = "UM"; name = "Maastricht University"; shortName = "UM"; organizationType = "root" }) +
        @($faculties | ForEach-Object { @{ organizationId = $_.code; name = $_.name; shortName = $_.code; organizationType = "faculty"; parentOrganizationId = "UM" } }))

    $sessions = @(
        @{ academicSessionId = "2026-2027"; name = "Academic year 2026-2027"; academicSessionType = "academic year"; startDate = "2026-09-01"; endDate = "2027-08-31" },
        @{ academicSessionId = "2026-P1"; name = "Period 1 2026-2027"; academicSessionType = "period"; startDate = "2026-09-01"; endDate = "2026-10-23"; parentAcademicSessionId = "2026-2027" },
        @{ academicSessionId = "2026-P2"; name = "Period 2 2026-2027"; academicSessionType = "period"; startDate = "2026-10-26"; endDate = "2026-12-18"; parentAcademicSessionId = "2026-2027" })
    Write-Csv "academic-sessions.csv" @("academicSessionId","name","academicSessionType","startDate","endDate","parentAcademicSessionId") $sessions

    Write-Csv "courses.csv" @("courseId","name","abbreviation","studyLoad","organizationId") (
        $courses | ForEach-Object { @{ courseId = $_.id; name = $_.name; abbreviation = $_.id; studyLoad = ([string]$_.ects).Replace(",", "."); organizationId = $_.fac } })

    # Every course runs once, alternating between period 1 and 2.
    $i = 0
    $offerings = @($courses | ForEach-Object {
        $period = if ($i++ % 2 -eq 0) { "2026-P1" } else { "2026-P2" }
        @{ offeringId = "$($_.id)-$period"; name = "$($_.name) ($period)"; courseId = $_.id; academicSessionId = $period; fac = $_.fac } })
    Write-Csv "course-offerings.csv" @("offeringId","name","courseId","academicSessionId") $offerings

    Write-Csv "persons.csv" @("personId","givenName","surnamePrefix","surname","mail") ($people | ForEach-Object {
        $prefix = if ($_.lastName -match '^((van der|van den|van|de) )') { $Matches[2] } else { "" }
        @{ personId = $_.attributes.personId[0]; givenName = $_.firstName; surnamePrefix = $prefix
           surname = ($_.lastName -replace '^(van der|van den|van|de) ', ''); mail = $_.email } })

    $enrolments = New-Object System.Collections.ArrayList
    function Enrol($person, $offering, $role) {
        [void]$enrolments.Add(@{ associationId = "$person-$offering-$role"; personId = $person; offeringId = $offering; role = $role })
    }
    # Fixed test accounts: student in three offerings (one with docent as lecturer), docent lecturer in two, beheerder coordinator.
    function Of($course) { ($offerings | Where-Object { $_.courseId -eq $course }).offeringId }
    Enrol "I6000001" (Of "SCI1004") "student"; Enrol "I6000001" (Of "KEN1130") "student"; Enrol "I6000001" (Of "PSY1001") "student"
    Enrol "P7000002" (Of "SCI1004") "lecturer"; Enrol "P7000002" (Of "ESS1001") "lecturer"
    Enrol "P7000001" (Of "SCI1004") "coordinator"
    foreach ($p in $people | Where-Object { $_.attributes.personId[0] -notin @("I6000001","P7000001","P7000002") }) {
        $id = $p.attributes.personId[0]; $dep = $p.attributes.department[0]
        $own = @($offerings | Where-Object { $_.fac -eq $dep })
        if ($id.StartsWith("I6")) {
            # Students: two offerings, preferably of their own faculty.
            $pool = if ($own.Count -ge 2) { $own } else { $offerings }
            $pool | Get-Random -Count 2 | ForEach-Object { Enrol $id $_.offeringId "student" }
        } elseif ($own.Count -gt 0 -and (Get-Random -Maximum 1.0) -lt 0.3) {
            Enrol $id ($own | Get-Random).offeringId (@("lecturer","lecturer","teaching-assistant") | Get-Random)
        }
    }
    Write-Csv "associations.csv" @("associationId","personId","offeringId","role") $enrolments
}

$studentCount = [math]::Round($Count * $StudentRatio)

# ================================================================ realm: demo
if ($Realm -in "all","demo") {
    $aff = @{ student  = @{ value = "student";    role = "student";    group = "Studenten" }
              employee = @{ value = "medewerker"; role = "medewerker"; group = "Medewerkers" } }
    $fixed = @(
        @{ u="alice"; f="Alice"; l="Admin";  roles=@("admin","editor","viewer","medewerker"); groups=@("/Beheer","/Medewerkers"); aff="medewerker"; dep="ICTS" },
        @{ u="bob";   f="Bob";   l="Editor"; roles=@("editor","viewer","medewerker");         groups=@("/Redactie","/Medewerkers"); aff="medewerker"; dep="FASoS" },
        @{ u="carol"; f="Carol"; l="Viewer"; roles=@("viewer","student");                     groups=@("/Studenten");               aff="student"; dep="FSE" }
    )
    $realmObj = [ordered]@{
        realm = "demo"; enabled = $true; displayName = "Demo (lokaal)"
        registrationAllowed = $false; loginWithEmailAllowed = $true; sslRequired = "none"
        roles = @{ realm = @(
            @{ name = "admin"; description = "Beheerder" }, @{ name = "editor"; description = "Kan inhoud bewerken" },
            @{ name = "viewer"; description = "Alleen lezen" }, @{ name = "medewerker"; description = "Medewerker van de instelling" },
            @{ name = "student"; description = "Student van de instelling" }) }
        groups = @(
            @{ name = "Beheer"; realmRoles = @("admin") }, @{ name = "Redactie"; realmRoles = @("editor") },
            @{ name = "Medewerkers"; realmRoles = @("medewerker") }, @{ name = "Studenten"; realmRoles = @("student") })
        components = (New-UserProfile @("medewerker","student"))
        users = (New-Users $fixed $aff)
        clients = @(
            [ordered]@{ clientId = "demo-app"; name = "Demo applicatie (publieke SPA/CLI-client)"; enabled = $true; publicClient = $true
                standardFlowEnabled = $true; directAccessGrantsEnabled = $true
                redirectUris = @("http://localhost:3000/*","http://localhost:5173/*","http://localhost:8081/*"); webOrigins = @("+")
                attributes = @{ "pkce.code.challenge.method" = "S256" }; protocolMappers = $userMappers },
            [ordered]@{ clientId = "demo-api"; name = "Demo API (confidential client)"; enabled = $true; publicClient = $false
                secret = "demo-api-secret"; serviceAccountsEnabled = $true; standardFlowEnabled = $true; directAccessGrantsEnabled = $true
                redirectUris = @("http://localhost:8081/*"); webOrigins = @("+"); protocolMappers = $userMappers })
    }
    Write-Realm $realmObj "demo-realm.json"
}

# ================================================================ realm: doctrina
if ($Realm -in "all","doctrina") {
    # Rollen en affiliation volgen OOAPI / eduPersonAffiliation-waarden (employee, student)
    $aff = @{ student  = @{ value = "student";  role = "student";  group = "Studenten" }
              employee = @{ value = "employee"; role = "employee"; group = "Medewerkers" }
              # UM-stijl nummers: studenten I6 + 6 cijfers, medewerkers P7 + 6 cijfers; ook als personId
              numbers  = @{ studentPrefix = "I6"; employeePrefix = "P7"; personId = $true } }
    $fixed = @(
        @{ u="beheerder"; f="Beheer"; l="Doctrina"; roles=@("admin","employee"); groups=@("/Beheerders","/Medewerkers"); aff="employee"; dep="ICTS"; nr="P7000001" },
        @{ u="docent";    f="Dirk";   l="Docent";   roles=@("employee");         groups=@("/Medewerkers");               aff="employee"; dep="FASoS"; nr="P7000002" },
        @{ u="student";   f="Sanne";  l="Student";  roles=@("student");          groups=@("/Studenten");                 aff="student"; dep="FSE"; nr="I6000001" }
    )
    $apiScopes = @("doctrina.persons.read","doctrina.persons.write","doctrina.courses.read","doctrina.courses.write","doctrina.import")

    $realmObj = [ordered]@{
        realm = "doctrina"; enabled = $true; displayName = "Doctrina (dev)"
        registrationAllowed = $false; loginWithEmailAllowed = $true; resetPasswordAllowed = $true; rememberMe = $true
        sslRequired = "none"; internationalizationEnabled = $true; supportedLocales = @("nl","en"); defaultLocale = "nl"
        accessTokenLifespan = 300; ssoSessionIdleTimeout = 3600; ssoSessionMaxLifespan = 36000
        smtpServer = [ordered]@{ host = "mailpit"; port = "1025"; from = "noreply@doctrina.example.test"; fromDisplayName = "Doctrina (dev)"; ssl = "false"; starttls = "false"; auth = "false" }
        roles = [ordered]@{
            realm = @(
                @{ name = "admin";    description = "Doctrina-beheerder (o.a. Import.Execute)" },
                @{ name = "employee"; description = "Medewerker (OOAPI affiliation: employee)" },
                @{ name = "student";  description = "Student (OOAPI affiliation: student)" })
            client = [ordered]@{
                # Application permissions voor systemen (client credentials) - Entra: app roles met allowedMemberTypes = Application
                "doctrina-api" = @($apiScopes | ForEach-Object { @{ name = $_; description = "API-permissie $_" } })
            } }
        groups = @(
            @{ name = "Beheerders"; realmRoles = @("admin") },
            @{ name = "Medewerkers"; realmRoles = @("employee") },
            @{ name = "Studenten"; realmRoles = @("student") })
        components = (New-UserProfile @("employee","student"))
        users = [System.Collections.ArrayList](New-Users $fixed $aff)
        clients = @(
            # Blazor Web App (Interactive Server): confidential client, authorization code + PKCE
            [ordered]@{ clientId = "doctrina-web"; name = "Doctrina web"; enabled = $true; publicClient = $false
                secret = "doctrina-web-secret"; standardFlowEnabled = $true; directAccessGrantsEnabled = $false; serviceAccountsEnabled = $false
                rootUrl = "https://localhost:5001"; baseUrl = "/"
                redirectUris = @("https://localhost:5001/signin-oidc","http://localhost:5000/signin-oidc")
                attributes = [ordered]@{ "pkce.code.challenge.method" = "S256"
                    "post.logout.redirect.uris" = "https://localhost:5001/signout-callback-oidc##http://localhost:5000/signout-callback-oidc##https://localhost:5001/##http://localhost:5000/" }
                webOrigins = @("+")
                protocolMappers = @($userMappers + (M-Attr "personId" "personId") + (M-RealmRolesFlat) + (M-Oid) + (M-Audience "doctrina-api")) },
            # De API als resource (audience); geen eigen login-flows
            [ordered]@{ clientId = "doctrina-api"; name = "Doctrina API (resource)"; enabled = $true; publicClient = $false
                secret = "doctrina-api-secret"; standardFlowEnabled = $false; directAccessGrantsEnabled = $false; serviceAccountsEnabled = $false; bearerOnly = $false },
            # Voorbeeld-systeemclient (SIS-koppeling): client credentials met API-permissies
            [ordered]@{ clientId = "doctrina-sis-sync"; name = "SIS-koppeling (client credentials)"; enabled = $true; publicClient = $false
                secret = "doctrina-sis-sync-secret"; standardFlowEnabled = $false; directAccessGrantsEnabled = $false; serviceAccountsEnabled = $true
                protocolMappers = @((M-ClientRolesFlat "doctrina-api"), (M-Audience "doctrina-api")) },
            # Hulpclient voor tests/scripts (password grant) - alleen dev
            [ordered]@{ clientId = "doctrina-dev-cli"; name = "Dev CLI (password grant, alleen lokaal)"; enabled = $true; publicClient = $true
                standardFlowEnabled = $false; directAccessGrantsEnabled = $true
                protocolMappers = @($userMappers + (M-Attr "personId" "personId") + (M-RealmRolesFlat) + (M-Oid) + (M-Audience "doctrina-api")) }
        )
    }
    # Service-account van de SIS-client krijgt API-permissies
    [void]$realmObj.users.Add([ordered]@{
        username = "service-account-doctrina-sis-sync"; enabled = $true; serviceAccountClientId = "doctrina-sis-sync"
        clientRoles = @{ "doctrina-api" = @("doctrina.persons.read","doctrina.persons.write","doctrina.courses.read","doctrina.courses.write","doctrina.import") }
    })
    Write-Realm $realmObj "doctrina-realm.json"
    Write-AcademicCsv ($realmObj.users | Where-Object { $_.attributes -and $_.attributes.personId })
}
Write-Host "Per realm: $studentCount studenten + $($Count - $studentCount) medewerkers + 3 vaste testaccounts."
