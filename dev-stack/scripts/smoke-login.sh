#!/usr/bin/env bash
# Smoke test: really sign in to Doctrina.Host through Keycloak (dev-stack) + API with client credentials.
# Requires: dev-stack is running (docker compose up -d) and Doctrina.Host is running on https://localhost:5001.
# Usage (Git Bash):  bash dev-stack/scripts/smoke-login.sh
set -euo pipefail
APP=${APP:-https://localhost:5001}
KC=${KC:-http://localhost:8080/realms/doctrina}
fail=0
check() { if [[ "$2" == "$3" ]]; then echo "OK   $1"; else echo "FOUT $1 (verwacht '$3', kreeg '$2')"; fail=1; fi; }

login() { # $1 user  $2 password  $3 cookie jar  $4 path
  local page action post target args=()
  page=$(curl -sk -c "$3" -b "$3" -L "$APP$4")
  action=$(grep -o 'id="kc-form-login"[^>]*action="[^"]*"' <<<"$page" | sed 's/.*action="//;s/"$//;s/&amp;/\&/g')
  [[ -n "$action" ]] || { echo "FOUT geen Keycloak-loginformulier"; return 1; }
  # Keycloak sets Secure cookies, also on http://localhost; browsers send them along, curl does not.
  sed -i 's/^\(\(#HttpOnly_\)\?localhost\t[A-Z]*\t[^\t]*\t\)TRUE/\1FALSE/' "$3"
  post=$(curl -sk -c "$3" -b "$3" --data-urlencode "username=$1" --data-urlencode "password=$2" "$action")
  # response_mode=form_post: self-submitting form, which a browser posts automatically
  target=$(grep -oE '<FORM [^>]*ACTION="[^"]*"' <<<"$post" | sed 's/.*ACTION="//;s/"$//')
  [[ -n "$target" ]] || { echo "FOUT inloggen als $1 mislukt"; return 1; }
  while read -r name value; do args+=(--data-urlencode "$name=$value"); done < <(
    grep -oE '<INPUT TYPE="HIDDEN" NAME="[^"]*" VALUE="[^"]*"' <<<"$post" | sed -E 's/.*NAME="([^"]*)" VALUE="([^"]*)"/\1 \2/')
  curl -sk -c "$3" -b "$3" -L -o /dev/null "${args[@]}" "$target"
}
field() { grep -o "data-testid=\"$1\">[^<]*" | sed 's/.*>//'; }

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

echo "== student (via /profile)"
login student student "$tmp/s" /profile
html=$(curl -sk -b "$tmp/s" "$APP/profile")
check "naam op /profile" "$(field name <<<"$html")" "Sanne Student"
check "personId op /profile" "$(field person-id <<<"$html")" "I6000001"
check "issuer" "$(field issuer <<<"$html")" "$KC"
check "beheerder?" "$(field is-admin <<<"$html")" "no"
check "API weigert sessiecookie" "$(curl -sk -b "$tmp/s" -o /dev/null -w '%{http_code}' "$APP/api/v1/me")" "401"

echo "== beheerder"
login beheerder beheerder "$tmp/b" /profile
check "beheerder?" "$(curl -sk -b "$tmp/b" "$APP/profile" | field is-admin)" "yes"

echo "== nepgebruiker femke.coenen"
login femke.coenen test123 "$tmp/f" /profile
html=$(curl -sk -b "$tmp/f" "$APP/profile")
check "naam" "$(field name <<<"$html")" "Femke Coenen"
check "personId" "$(field person-id <<<"$html")" "I6834263"

echo "== uitloggen (student)"
tokenf=$(curl -sk -c "$tmp/s" -b "$tmp/s" "$APP/" | grep -o 'name="__RequestVerificationToken" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
loc=$(curl -sk -c "$tmp/s" -b "$tmp/s" -o /dev/null -w '%{redirect_url}' --data-urlencode "__RequestVerificationToken=$tokenf" --data-urlencode "returnUrl=/" "$APP/sign-out")
check "doorgestuurd naar Keycloak-logout" "${loc%%\?*}" "$KC/protocol/openid-connect/logout"
end=$(curl -sk -c "$tmp/s" -b "$tmp/s" -o /dev/null -w '%{redirect_url}' "$loc")
check "Keycloak stuurt terug naar Doctrina" "${end%%\?*}" "$APP/signout-callback-oidc"
check "/profile vraagt weer om inloggen" "$(curl -sk -b "$tmp/s" -o /dev/null -w '%{http_code}' "$APP/profile")" "302"

echo "== systeem doctrina-sis-sync (client credentials)"
cc=$(curl -s -d grant_type=client_credentials -d client_id=doctrina-sis-sync -d client_secret=doctrina-sis-sync-secret "$KC/protocol/openid-connect/token" | sed 's/.*"access_token":"\([^"]*\)".*/\1/')
check "/api/v1/me met token" "$(curl -sk -H "Authorization: Bearer $cc" "$APP/api/v1/me" | grep -o '"client":"[^"]*"')" '"client":"doctrina-sis-sync"'
check "/api/v1/me zonder token" "$(curl -sk -o /dev/null -w '%{http_code}' "$APP/api/v1/me")" "401"

exit $fail
