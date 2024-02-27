# import time
# import datetime
# from datetime import timezone
# from zoneinfo import ZoneInfo
# from dateutil import tz
# import json
# import numcodecs as ncd
# import cartopy.crs as ccrs
from datetime import datetime, timedelta
import logging
import s3fs
import numpy as np
import zarr
import xarray as xr
import pandas as pd
import dask
import sys

# create encoder for turning DataArray objects into lists
def np_encoder(object):
    """
    Convert any numpy type to a generic type for json serialization.
    Parameters
    ----------
    object
    Object to be converted.
    Returns
    -------
    object
    Generic object or an unchanged object if not a numpy type
    """
    if isinstance(object, np.generic):
        return object.item()

# set up request URLs and arguments in proper format
request_url = "s3://noaa-nwm-retrospective-2-1-zarr-pds/chrtout.zarr"

subgroup_url = f"{request_url}/streamflow"

commid = sys.argv[1]
sdate = sys.argv[2]
edate = sys.argv[3]
outputFileName = sys.argv[4]

print("File Name: " + outputFileName)

print("ARGS: " + commid + " " + sdate + " " + edate + " " + outputFileName)

# corrects formatting whether one comid or multiple
ids = []
try:
    ids.append(int(commid))
except:
    coms = commid.split(',')
    if(len(coms) > 1):
        ids = []
        for c in coms:
            ids.append(int(c))

print("Starting to read zarr data from " + sdate + " to " + edate + " for comd ids: " + commid)

comids = ids
start_date = datetime.strptime(sdate, "%Y-%m-%d")
end_date = datetime.strptime(edate, "%Y-%m-%d")

print("Setting Up Dataset...")

# sets up file mapping system used to read the zarr data
fs = s3fs.S3FileSystem(anon=True)    

logging.info(f"Using NWM 2.1 URL: {request_url}")
logging.info(f"Request data for COMIDS: {comids}")
logging.info("Executing optimized nwm data call")
s3 = s3fs.S3FileSystem(anon=True)
store = s3fs.S3Map(root=request_url, s3=s3, check=False)

# opens zarr using the specified file store
ds = xr.open_zarr(store=store, consolidated=True, decode_times=True)
print("Reading Zarr Data...")

# slices a large chunk from the zarr dataset using the specified arguments
with dask.config.set(**{'array.slicing.split_large_chunks': True}):
    ds_velflow = ds.sel(feature_id=comids).sel(time=slice(
        f"{start_date.year}-{start_date.month}-{start_date.day}",
        f"{end_date.year}-{end_date.month}-{end_date.day}"
    )).load()

print("Finshed Reading Zarr Data.")
print("Streamflow and Velocity Dataset:")
print(ds_velflow)

# gets full time values as numpy array
dtcoords = ds_velflow.coords['time']
numpy_dtcoords = dtcoords.values

# gets streamflow and velocity values as a numpy array
streamvars = ds_velflow.data_vars['streamflow'].values
velvars = ds_velflow.data_vars['velocity'].values

print("Converting Data Format...")

# converts numpy arrays into a string arrays instead for easier printing
streamstring = []
for stm in streamvars:
    stmstring = '['
    for val in stm:
        stmstring = stmstring + str(val) + ', '
    streamstring.append(stmstring[:len(stmstring) - 2] + ']')

velstring = []
for vel in velvars:
    vstring = '['
    for val in vel:
        vstring = vstring + str(val) + ', '
    velstring.append(vstring[:len(vstring) - 2] + ']')

# creates new string array to contain each line to be printed to output file
dtstring = []
# dtstring.append(str(commid) + '\n')

# loops through time array along with a counter to create each line in a specified format
for i, dt in enumerate(numpy_dtcoords):
    ts = pd.to_datetime(str(dt)) 
    d = ts.strftime('%Y-%m-%d %H:%M:%S')

    datastr = streamstring[i] + '*' + velstring[i] + '*' + d + ' UTC\n'
    dtstring.append(datastr)

# writes the lines of the new string array to file
print("Writing Data to File " + outputFileName + " ...")

# currentTime = datetime.now().strftime("%Y-%m-%d %H")

# filename = commid + "_" + sdate + "_" + edate + "_" + currentTime +".txt"

with open(outputFileName, 'w') as output:
    output.writelines(dtstring)
# with open('output.txt', 'a') as output:
#     output.writelines(dtstring)