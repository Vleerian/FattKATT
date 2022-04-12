# FattKATT

A fancy C# port of
*Khron and Atagait's Trigger Tool*

## About

FattKATT is a simple script that takes a list of NationStates regions, sorts them by update order, and informs the user when
the game API reports they have updated.

## Usage

**DO NOT RUN TWO INSTANCES OF FATTKATT AT THE SAME TIME.**

FattKATT requires a list of regions to trigger on in `trigger_list.txt` - if this file does exist, the program will creeate it and prompt you to fill it out. Each trigger should be on it's own line.

FattKATT first will prompt you for your main nation - this is used exclusively to identify the current user of the script to NS' admin.

It will then ask how often it should request data from the NationStates API - it will not allow values beneath 600ms as that is the maximum speed permitted by the rate limit (One request every 0.6 seconds)

## Running FattKATT

I suggest running [url=https://github.com/Vleerian/FattKATT/releases/latest]The latest release[/url]

If you want to run directly from source, you will need the Dotnet 6.0 SDK in order to build the script.

You can build it by running
`dotnet build -C Release`

## Acknowledgments

The following people provided key contributions during the initial development process:

* The original version of KATT was programmed by [url=https://github.com/Khronion]Khronion[/url]

The following people also helped review and test KATT:

* Koth and Aav tested the multiplatform builds on Linux and Mac (respectively)
* Khron provided bug reports, as well as reviewing the code for NS legality issues

## Disclaimer

The software is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

Any individual using a script-based NationStates API tool is responsible for ensuring it complies with the latest
version of the [API Terms of Use](https://www.nationstates.net/pages/api.html#terms). KATT is designed to comply with
these rules, including the rate limit, as of April 21, 2019, under reasonable use conditions, but the authors are not
responsible for any unintended or erroneous program behavior that breaks these rules.

Never run more than one program that uses the NationStates API at once. Doing so will likely cause your IP address to
exceed the API rate limit, which will in turn cause both programs to fail.
