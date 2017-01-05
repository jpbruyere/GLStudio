# GLStudio [![Build Status](https://travis-ci.org/jpbruyere/GLStudio.svg?branch=master)](https://travis-ci.org/jpbruyere/GLStudio) [![Build Status Windows](https://ci.appveyor.com/api/projects/status/j387lo59vnov8jbc?svg=true)](https://ci.appveyor.com/project/jpbruyere/GLStudio)
OpenGL studio using Crow toolkit

Please report bugs and issues on [GitHub](https://github.com/jpbruyere/GLStudio/issues)

###Building from sources
```bash
git clone https://github.com/jpbruyere/GLStudio.git	# Download sources
cd GLStudio
git submodule update --init --recursive         # Get submodules
nuget restore                                   # restore nuget
xbuild  /p:Configuration=Release GLStudio.sln   # Compile
```
The resulting executable will be in **build/Release**.
