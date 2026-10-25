#!/bin/sh
# Build the x64 Windows hotfix DLL on macOS using LLVM-MinGW target libraries.
# Does not run tests. MSVC builds remain available through build-release.ps1.
set -eu

: "${LLVM_MINGW_ROOT:?Set LLVM_MINGW_ROOT to the unpacked LLVM-MinGW distribution}"
: "${MINHOOK_ROOT:?Set MINHOOK_ROOT to a MinHook source checkout}"
: "${LLD_PATH:?Set LLD_PATH to a native ld.lld executable}"
CLANG_C=${CLANG_C:-clang}
CLANG_CXX=${CLANG_CXX:-clang++}
repo=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
output=${1:-"$repo/dist/amd-24h2-preview/dwm_lut.dll"}
work=$(mktemp -d "${TMPDIR:-/tmp}/dwm-lut-cross.XXXXXX")
trap 'rm -rf "$work"' EXIT HUP INT TERM
mkdir -p "$(dirname -- "$output")"

target="$LLVM_MINGW_ROOT/x86_64-w64-mingw32"
cpp_headers=""
for candidate in "$target/include/c++/v1" "$LLVM_MINGW_ROOT/include/c++/v1" "$LLVM_MINGW_ROOT/generic-w64-mingw32/include/c++/v1"; do
    if [ -f "$candidate/vector" ]; then cpp_headers=$candidate; break; fi
done
[ -n "$cpp_headers" ] || { echo "LLVM-MinGW libc++ headers not found" >&2; exit 1; }
runtime=""
for candidate in "$LLVM_MINGW_ROOT"/lib/clang/*; do
    if [ -f "$candidate/lib/windows/libclang_rt.builtins-x86_64.a" ]; then runtime=$candidate; break; fi
done
[ -n "$runtime" ] || { echo "LLVM-MinGW compiler-rt library not found" >&2; exit 1; }

for source in buffer hook trampoline hde/hde64; do
    object=$(basename "$source")
    "$CLANG_C" --target=x86_64-w64-windows-gnu --sysroot="$target" \
        -O2 -DNDEBUG -D_WIN32_WINNT=0x0A00 -I "$MINHOOK_ROOT/include" \
        -c "$MINHOOK_ROOT/src/$source.c" -o "$work/$object.o"
done

"$CLANG_CXX" --target=x86_64-w64-windows-gnu --sysroot="$target" \
    -std=c++17 -O2 -g -gcodeview -DNDEBUG -DUNICODE -D_UNICODE -D_WIN32_WINNT=0x0A00 \
    -D_CRT_SECURE_NO_WARNINGS -D_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS \
    -fms-extensions -fexceptions -nostdinc++ -isystem "$cpp_headers" \
    -I "$MINHOOK_ROOT/include" -c "$repo/src/lutdwm/dllmain.cpp" -o "$work/dllmain.o"

"$CLANG_CXX" --target=x86_64-w64-windows-gnu --sysroot="$target" \
    --ld-path="$LLD_PATH" -stdlib=libc++ -shared -static \
    -resource-dir="$runtime" -rtlib=compiler-rt -unwindlib=libunwind \
    "$work/dllmain.o" "$work/buffer.o" "$work/hook.o" "$work/trampoline.o" "$work/hde64.o" \
    -L "$target/lib" -ld3d11 -ld3dcompiler -ldxgi -ldxguid -luuid -lpsapi -luser32 \
    -Wl,--no-insert-timestamp "-Wl,--pdb=$(dirname -- "$output")/dwm_lut.pdb" -o "$output"
echo "Built Windows x64 DLL: $output"
