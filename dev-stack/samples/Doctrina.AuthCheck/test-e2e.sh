#!/usr/bin/env bash
# End-to-end test: inloggen via het Keycloak-formulier in de Doctrina.AuthCheck-app + API met client credentials.
# Vereist: dev-stack draait, app draait op https://localhost:5001 (dotnet run --launch-profile https).
set -euo pipefail
APP=https://localhost:5001
KC=http://localhost:8080/realms/doctrina
fail=0
check() { if [[ "$2" == "$3" ]]; then echo "OK   $1"; else echo "FOUT $1 (verwacht '$3', kreeg '$2')"; fail=1; fi; }

login() { # $1 user $2 password $3 cookiejar
  local page action
  page=$(curl -sk -c "$3" -b "$3" -L "$APP/login")
  action=$(grep -o 'id="kc-form-login"[^>]*action="[^"]*"' <<<"$page" | sed 's/.*action="//;s/"$//;s/&amp;/\&/g')
  [[ -n "$action" ]] || { echo "FOUT geen Keycloak-loginformulier gevonden"; exit 1; }
  # Keycloak zet Secure-cookies, ook op http://localhost. Browsers sturen die mee (localhost = secure context), curl niet.
  sed -i 's/^\(\(#HttpOnly_\)\?localhost\t[A-Z]*\t[^\t]*\t\)TRUE/\1FALSE/' "$3"
  local post target args=()
  post=$(curl -sk -c "$3" -b "$3" --data-urlencode "username=$1" --data-urlencode "password=$2" "$action")
  # Keycloak antwoordt met een zelf-verzendend formulier (response_mode=form_post); een browser doet dit automatisch.
  target=$(grep -oE '<FORM [^>]*ACTION="[^"]*"' <<<"$post" | sed 's/.*ACTION="//;s/"$//')
  [[ -n "$target" ]] || { echo "FOUT inloggen als $1 mislukt (geen form_post-antwoord)"; return 1; }
  while read -r name value; do args+=(--data-urlencode "$name=$value"); done < <(
    grep -oE '<INPUT TYPE="HIDDEN" NAME="[^"]*" VALUE="[^"]*"' <<<"$post" | sed -E 's/.*NAME="([^"]*)" VALUE="([^"]*)"/\1 \2/')
  curl -sk -c "$3" -b "$3" -L -o /dev/null "${args[@]}" "$target"
}

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

echo "== Gebruiker 'student' (browserflow: code + PKCE)"
login student student "$tmp/s"
check "ingelogd als Sanne Student" "$(curl -sk -b "$tmp/s" $APP/ | grep -o 'Ingelogd als <b>[^<]*' | sed 's/.*<b>//')" "Sanne Student"
check "student mag /admin niet (403)" "$(curl -sk -b "$tmp/s" -o /dev/null -w '%{http_code}' $APP/admin)" "403"
tok=$(curl -sk -b "$tmp/s" $APP/token)
check "API met user-token van student: /api/v1/whoami" "$(curl -sk -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $tok" $APP/api/v1/whoami)" "200"
check "API met user-token van student: /api/v1/persons geweigerd" "$(curl -sk -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $tok" $APP/api/v1/persons)" "403"

echo "== Gebruiker 'beheerder'"
login beheerder beheerder "$tmp/b"
check "beheerder mag /admin" "$(curl -sk -b "$tmp/b" -o /dev/null -w '%{http_code}' $APP/admin)" "200"

echo "== Nepgebruiker (femke.coenen / test123)"
login femke.coenen test123 "$tmp/f"
check "ingelogd als Femke Coenen" "$(curl -sk -b "$tmp/f" $APP/ | grep -o 'Ingelogd als <b>[^<]*' | sed 's/.*<b>//')" "Femke Coenen"

echo "== Systeem 'doctrina-sis-sync' (client credentials)"
cc=$(curl -s -d grant_type=client_credentials -d client_id=doctrina-sis-sync -d client_secret=doctrina-sis-sync-secret $KC/protocol/openid-connect/token | sed 's/.*"access_token":"\([^"]*\)".*/\1/')
check "/api/v1/persons met permissie doctrina.persons.read" "$(curl -sk -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $cc" $APP/api/v1/persons)" "200"
check "/api/v1/persons zonder token" "$(curl -sk -o /dev/null -w '%{http_code}' $APP/api/v1/persons)" "401"

exit $fail
