# File primarily used for testing before building

# import time
# import datetime
from datetime import timezone, datetime, timedelta
from zoneinfo import ZoneInfo
from dateutil import tz
import json
import logging
import s3fs
import numcodecs as ncd
import numpy as np
import zarr
import xarray as xr
import cartopy.crs as ccrs
import pandas as pd
import dask
import sys
# import warnings
# import metpy
# import shutil
# import fsspec
# from dask.distributed import Client, LocalCluster
# from dask.distributed import Client, progress, LocalCluster, performance_report
# warnings.filterwarnings("ignore", category=ResourceWarning)
# logging.getLogger('boto3').setLevel(logging.CRITICAL)
# boto3.set_stream_logger('botocore', logging.INFO)
# boto3.set_stream_logger('s3fs', logging.INFO)
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


request_url = "s3://noaa-nwm-retrospective-2-1-zarr-pds/chrtout.zarr"

subgroup_url = f"{request_url}/streamflow"

fs = s3fs.S3FileSystem(anon=True)    

# ds = xr.open_mfdataset([s3fs.S3Map(url, s3=fs) for url in [request_url, subgroup_url]], engine='zarr')

# ds = xr.open_zarr(fsspec.get_mapper(request_url, anon=True), consolidated=True)

# comids = [6275977, 6277141]

commid = sys.argv[1]
print(commid)
sdate = sys.argv[2]
print(sdate)
edate = sys.argv[3]
print(edate)

ids = [commid]

coms = commid.split(',')
if(len(coms) > 1):
    ids = []
    for c in coms:
        ids.append(int(c))

print(ids)
comids = ids
start_date = datetime.strptime(sdate, "%Y-%m-%d")
end_date = datetime.strptime(edate, "%Y-%m-%d") + timedelta(days=1)
# comids = [6275977,6275981,6275987,6276027,6276045,6276067,6277079,6277141]
# start_date = datetime.strptime("2001-05-06", "%Y-%m-%d")
# end_date = datetime.strptime("2002-05-06", "%Y-%m-%d") + timedelta(days=1)

logging.info(f"Using NWM 2.1 URL: {request_url}")
logging.info(f"Request data for COMIDS: {comids}")
logging.info("Executing optimized nwm data call")
s3 = s3fs.S3FileSystem(anon=True)
store = s3fs.S3Map(root=request_url, s3=s3, check=False)

ds = xr.open_zarr(store=store, consolidated=True, decode_times=True)

# with dask.config.set(**{'array.slicing.split_large_chunks': True}):
#     ds_streamflow = ds['streamflow'].sel(feature_id=comids).sel(time=slice(
#         f"{start_date.year}-{start_date.month}-{start_date.day}",
#         f"{end_date.year}-{end_date.month}-{end_date.day}"
#     )).load()

# print("Streamflow:")
# print(ds_streamflow)

# with dask.config.set(**{'array.slicing.split_large_chunks': True}):
#     ds_velocity = ds['velocity'].sel(feature_id=comids).sel(time=slice(
#         f"{start_date.year}-{start_date.month}-{start_date.day}",
#         f"{end_date.year}-{end_date.month}-{end_date.day}"
#     )).load()

# print("Velocity:")
# print(ds_velocity)

with dask.config.set(**{'array.slicing.split_large_chunks': True}):
    ds_velflow = ds.sel(feature_id=comids).sel(time=slice(
        f"{start_date.year}-{start_date.month}-{start_date.day}",
        f"{end_date.year}-{end_date.month}-{end_date.day}"
    )).load()

print("Velocity and Streamflow:")
print(ds_velflow)

new_numpy_ndarray = ds_velflow.as_numpy()
np.set_printoptions(threshold=np.Inf)
# print(new_numpy_ndarray)

dtcoords = ds_velflow.coords['time']

# gets full time values as numpy array
numpy_dtcoords = dtcoords.values
# print(numpy_dtcoords)

streamvars = ds_velflow.data_vars['streamflow'].values
velvars = ds_velflow.data_vars['velocity'].values

# gagedat = ds_velflow.coords['gage_id']
# gageval = gagedat.values

# print('gage data:')
# print(gagedat)
# print(gageval)

logging.info("NWM data request completed")

streamstring = []
for stm in streamvars:
    stmstring = '['
    for val in stm:
        stmstring = stmstring + str(val) + ', '
    streamstring.append(stmstring[:len(stmstring) - 2] + ']')
    # streamstring.append(str(stm))

velstring = []
for vel in velvars:
    vstring = '['
    for val in vel:
        vstring = vstring + str(val) + ', '
    velstring.append(vstring[:len(vstring) - 2] + ']')
    # velstring.append(str(vel))

dtstring = []
dtstring.append(str(comids) + '\n')

for i, dt in enumerate(numpy_dtcoords):
    ts = pd.to_datetime(str(dt)) 

    # d = to_local(ts)
    d = ts.strftime('%Y-%m-%d %H:%M:%S')
    # temptime = datetime.strptime(d, '%Y-%m-%d %H:%M:%S')
    # temptime = temptime.replace(tzinfo=from_zone)
    
    # localtime = temptime.astimezone(to_zone)
    # ts2 = pd.to_datetime(str(localtime))
    # d2 = ts.strftime('%Y-%m-%d %H:%M:%S')
    datastr = streamstring[i] + '*' + velstring[i] + '*' + d + ' UTC\n'

    dtstring.append(datastr)

# print(dtstring)

with open('test2.json', 'w') as json_file:
    json.dump(ds_velflow.to_dict(data='list'), json_file, default=np_encoder)
    json_file.close()

# with open('test2.json', 'w') as json_file:
#     json.dump(np.ndarray.tolist(numpy_dtcoords), json_file)

with open('test2.txt', 'w') as output:
    output.writelines(dtstring)
#     output.close()

# print(new_numpy_ndarray)

# with open('test3.txt', 'w') as output:
#     output.write(new_numpy_ndarray.to_dict(data='list'))
# with open('test.txt', 'w') as output:
#     output.write(json.dumps(data.to_dict(data='list', encoding=False)))

# print(ds[var])


# warnings.filterwarnings("ignore", category=ResourceWarning)
# if not scheduler:
#     scheduler = os.getenv('DASK_SCHEDULER', "127.0.0.1:8786")
# # scheduler = LocalCluster()
# client = Client(scheduler)
# request_url = nwm_21_url
# request_variables = copy.copy(variables)

# if self.waterbody:
#     logging.info("Requesting NWM waterbody data")
#     request_url = nwm_21_wb_url
#     request_variables = copy.copy(wb_variables)



# else:
#     logging.info("Executing non-optimized nwm data call")
#     ds = xr.open_zarr(fsspec.get_mapper(request_url, anon=True), consolidated=True)
#     comid_check = []
#     missing_comids = []
# #             if not self.waterbody:
# #                 for c in self.comids:
# #                     try:
# #                         test = ds["streamflow"].sel(feature_id=c).sel(time=slice("2010-01-01", "2010-01-01"))
# #                         comid_check.append(c)
# #                     except KeyError:
# #                         missing_comids.append(c)
# #                 if len(missing_comids) > 0:
# #                     self.output.add_metadata("missing_comids", ", ".join(missing_comids))
#     with dask.config.set(**{'array.slicing.split_large_chunks': True}):
#         ds_streamflow = ds[request_variables].sel(feature_id=self.comids).sel(time=slice(
#             f"{self.start_date.year}-{self.start_date.month}-{self.start_date.day}",
#             f"{self.end_date.year}-{self.end_date.month}-{self.end_date.day}"
#         )).load(optimize_graph=False, traverse=False)


# self.output.add_metadata("retrieval_timestamp", datetime.datetime.now().isoformat())
# self.output.add_metadata("source_url", nwm_url)
# self.output.add_metadata("variables", ", ".join(request_variables))


# scheduler.close()
# client.close()

# projection = ccrs.LambertConformal(central_longitude=262.5, 
#                                    central_latitude=38.5, 
#                                    standard_parallels=(38.5, 38.5),
#                                     globe=ccrs.Globe(semimajor_axis=6371229,
#                                                      semiminor_axis=6371229))

# ds = ds.rename(projection_x_coordinate="x", projection_y_coordinate="y")
# ds = ds.metpy.assign_crs(projection.to_cf())
# ds = ds.metpy.assign_latitude_longitude()    
# ds


# s3 = s3fs.S3FileSystem(anon=True)

# def retrieve_data(s3_url):
#     # with s3.open(s3_url, 'rb') as compressed_data:
#     #     buffer = ncd.blosc.decompress(compressed_data.read())

#     #     dtype = "<f2"
#     #     if ("surface/PRES" in s3_url
#     #         or "mean_sea_level/MSLMA" in s3_url
#     #         or "0C_isotherm/PRES" in s3_url): # Pressures above 1000hPa use a larger data type
#     #         dtype = "<f4"

#     #     chunk = np.frombuffer(buffer, dtype=dtype)
        
#     #     gridpoints_in_chunk = 150*150
#     #     number_hours = len(chunk)//gridpoints_in_chunk

#     #     if number_hours == 1: # analysis data is 2d
#     #         data_array = np.reshape(chunk, (150, 150))
#     #     else: # forecast data is 3d but of varying length in time
#     #         data_array = np.reshape(chunk, (number_hours, 150, 150))

#     return s3.open(s3_url, 'feature_id')

# chunk_data = retrieve_data(url)
# print(chunk_data)

# time.sleep(10)


# import numpy as np
# import zarr
# from numcodecs import JSON

# # slightly contrived example of an object array
# x = np.empty(1, dtype=object)
# x[0] = [0 , 2, 1]

# za = zarr.open_array('path/to/zarr', shape=x.shape, dtype=object, object_codec=JSON())

# print(zarr.open_array(store)[:][0])  # --> ['foo', 2, 'bar']
# 
# 
# 
# 
# print("Hello World")

# with open('test.txt', 'w') as output:
#     output.write("Testing Output Complete.")