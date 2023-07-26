package example

import future.keywords.if

# METADATA
# entrypoint: true
allow if {
    true
}

# METADATA
# entrypoint: true
deny if {
    false
}

# METADATA
# entrypoint: true
allow_path if {
    startswith("/path/allow", input.path)
}