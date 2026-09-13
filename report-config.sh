#!/bin/sh
# Report which pieces of configuration actually arrived, by NAME only — never a value. A dropped
# variable otherwise surfaces hundreds of lines later as an empty connection string or a null
# options section, which is how one lost environment block cost a day of looking at CORS errors.
#
# Run as `report-config.sh <command...>`: it prints the report and then execs the command, so it
# works both on its own and as the thing `infisical run` execs.
present=""
missing=""
for name in ConnectionStrings__DefaultConnection JwtSettings__SecretKey ClientSettings__BaseUrl \
            ObjectStorage__Endpoint EmailSettings__Host Cors__AllowedOrigins__0 ASPNETCORE_URLS
do
    eval "value=\${$name}"
    if [ -n "$value" ]; then
        present="$present $name"
    else
        missing="$missing $name"
    fi
done

echo "config: present:${present:- (none)}" >&2
if [ -n "$missing" ]; then
    echo "config: MISSING:$missing" >&2
fi

exec "$@"
