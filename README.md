# RomLauncher
Cross platform tool for launching roms. Copies to a target directory, asks if an MSU is desired, then launch the rom. Uses the [MSU Randomizer](https://github.com/MattEqualsCoder/MSURandomizer) to shuffle MSUs.

## Usage
Simply run it via commandline like the following:

```
.\RomLauncher path_to_rom.sfc
```

You can use this as the default application for sfc files to be able to easily apply MSUs to them, though you'll need to make sure the terminal is visibile.

## Settings File
When running the first time, it'll generate a rom-launcher.yml file that will need to be edited. Below is an example of the settings.

```
# The parent directory for all of the MSUs
MsuPath: '/home/matt/SMZ3/MSUs'

# The parent folder where all roms will be copied to to apply the MSU
TargetPath: '/home/matt/SMZ3/Roms'

# The application or script to call for launching the rom. If none is specified, the default application for sfcs will be used
LaunchApplication: '/home/matt/SMZ3/retroarch.sh'

# The arguments to use for the launching the rom. %rom% in the string will be replaced with the rom path. If nothing is provided, the rom path will be used.
LaunchArguments: ''

# The list of MSU types that are desired. Used to clean up the MSU type list.
MsuTypeFilter:
  - SMZ3 Combo Randomizer
  - SMZ3 Classic (Metroid First)
  - A Link to the Past
```
