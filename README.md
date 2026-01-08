# rF2AllVehiclas.Plugin

SimHub plugin to use last driven car to preserve seat, mirror, and FFB multipler settings, for all other cars of same kind.

## Background

Adjusting _seat_, _mirrors_ and/or _FFB multiplier_ is only saved for the current vehicle/livery combination. I.e. when selecting the same car but a different livery you need to set the mirrors and FFB once again.

## Solution

Monitor the last driven car and  apply/copy settings to the all other cars of the same type into the `all_vehicles.ini` file.

## Installation

Copy DLL into SimHub installation folder.

## Similar Cars Strategy

Cars are identified by its name and version inside `all_vehicles.ini`. We currently ignore the version ensuring _all_ cars of same type is preserved/updated. 

### Backups

Backups, 10 of them, of `all_vehicles.ini` is created when the file is updated.

## Issues

Please report issues on github, https://github.com/mattno/rF2AllVehiclas.Plugin/issues.
