#!/usr/bin/env bash
set -e

# Nombre del archivo de salida
OUT="CODEBASE.md"

# Extensiones de código que quieres incluir
EXTENSIONS=("cs" "csproj" "json" "md")

# Carpeta raíz desde la que generar (por defecto, la actual)
ROOT_DIR="."

echo "# Código completo del proyecto" > "$OUT"
echo "" >> "$OUT"
echo "_Generado el $(date)_ " >> "$OUT"
echo "" >> "$OUT"

# Función para añadir un archivo al MD
append_file() {
  local file="$1"
  local rel
  rel=$(realpath --relative-to="$ROOT_DIR" "$file" 2>/dev/null || echo "$file")

  echo "## \`$rel\`" >> "$OUT"
  echo "" >> "$OUT"

  # Deducción simple del lenguaje para el bloque ``` ```
  case "$file" in
    *.cs)    lang="csharp" ;;
    *.csproj) lang="xml" ;;
    *.json)  lang="json" ;;
    *.md)    lang="markdown" ;;
    *)       lang="" ;;
  esac

  if [ -n "$lang" ]; then
    echo '```'"$lang" >> "$OUT"
  else
    echo '```' >> "$OUT"
  fi

  cat "$file" >> "$OUT"
  echo "" >> "$OUT"
  echo '```' >> "$OUT"
  echo "" >> "$OUT"
}

# Buscar y añadir archivos
for ext in "${EXTENSIONS[@]}"; do
  while IFS= read -r -d '' f; do
    append_file "$f"
  done < <(find "$ROOT_DIR" -type f -name "*.${ext}" -print0)
done

echo "✅ CODEBASE.md generado en $(realpath "$OUT")"
