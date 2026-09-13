#!/bin/sh
# Pull configuration from Infisical when a machine identity is present, then exec the API with
# those secrets in its environment. .NET's configuration binding is untouched: every secret still
# arrives as an ordinary environment variable, so `JwtSettings__SecretKey` binds exactly as before
# and no application code knows the vault exists.
#
# When the credentials are absent the API is exec'd directly against the container environment.
# That fallback is deliberate and is what makes the cutover reversible: unset the three INFISICAL_*
# variables and the container goes back to Dokploy's own environment block without a rebuild.
set -e

if [ -z "${INFISICAL_UNIVERSAL_AUTH_CLIENT_ID}" ] || [ -z "${INFISICAL_UNIVERSAL_AUTH_CLIENT_SECRET}" ]; then
    echo "entrypoint: no Infisical machine identity configured; using the container environment" >&2
    exec /usr/local/bin/report-config.sh "$@"
fi

# Fail loudly rather than silently fetching from the wrong project or environment. A typo here
# would otherwise start the API with an empty configuration and fail much further downstream.
if [ -z "${INFISICAL_PROJECT_ID}" ]; then
    echo "entrypoint: INFISICAL_PROJECT_ID is required when a machine identity is set" >&2
    exit 1
fi

# --plain --silent prints just the token. INFISICAL_API_URL points both this call and `run` at the
# self-hosted vault, which is why neither needs --domain.
INFISICAL_TOKEN="$(infisical login --method=universal-auth --plain --silent)"
export INFISICAL_TOKEN

echo "entrypoint: loading secrets from Infisical (${INFISICAL_PROJECT_ID}, ${INFISICAL_ENVIRONMENT:-prod})" >&2

# report-config.sh runs *inside* `infisical run`, so it describes the environment the API actually
# receives — the container's variables with the vault's merged in — not the one before the merge.
exec infisical run \
    --projectId="${INFISICAL_PROJECT_ID}" \
    --env="${INFISICAL_ENVIRONMENT:-prod}" \
    -- /usr/local/bin/report-config.sh "$@"
