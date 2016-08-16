set -e
MONO_PATH=/Applications/Unity/Unity.app/Contents/Frameworks/Mono
php ../bhl -D USER_SOURCES=bindings.cs run --dir=. --result=tmp/bhl.bytes --cache_dir=tmp --error=tmp/bhl.err
php ../bhl build_back_dll $MONO_PATH/bin/gmcs  
$MONO_PATH/bin/gmcs -r:../bhl_back.dll -out:example.exe example.cs
MONO_PATH=$MONO_PATH/lib/mono/2.0/:../ $MONO_PATH/bin/mono example.exe
