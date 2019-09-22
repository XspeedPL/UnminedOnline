# uNmINeD Online
Injects HTTP loading capabilities into uNmINeD.

In short, allows server owners to expose an API to use with a patched version of uNmINeD to view online worlds.

# The API
In order for a patched uNmINeD to work three elements are necessary on the server side:
1. Worlds list API
   - Accepts no arguments
   - Returns a list of accessible `/region` directories, one per line
2. Region listing API
   - Accepts a single argument, a directory returned from the worlds list API
   - Returns a list of accessible `r.*.mca` files, in `<filemtime>\t<name>` format, one per line
3. Region file access API
   - Accepts a single argument, a path to a file obtained from combining directory from worlds list API and a file name from the region listing API
   - Returns a valid stream of a Minecraft region file

# Example server setup
For the sake of simplicity this example uses PHP, but it does not matter what is used from the client side, as long as the exposed API conforms to the specification above.
1. Set up a PHP-capable WWW hosting software
2. Set the document root path inside the server world save directory
3. Create a `worlds.txt` file in the document root with following contents:
```
region
DIM-1/region
DIM1/region
```
4. Copy the `list.php` from this repository into the same directory

# Client setup
1. Download uNmINeD and extract
2. Download uNmINeD Online and extract into the same directory
3. Run `Unmined.Patch.exe`
4. Configure the server API settings in the `x_config.ini` file
5. Run `unmined.exe`
6. A new save folder should appear with label "RemoteWorld"
7. Select a dimension from the list in the folder
