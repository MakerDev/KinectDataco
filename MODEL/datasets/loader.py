import io

import os
from PIL import Image
import numpy
from numpy.core.fromnumeric import shape
import torch
from scipy.interpolate import interp1d


def image_resampling(video, frame_target, longest_frames):
    expanded_video = []
    final_video = []
    missing_length = longest_frames - len(video)

    # MATCH ALL VIDEOS TO Longest_frames

    add_front_length = missing_length // 2
    add_back_length = missing_length - add_front_length
    for i in range(add_front_length):
        expanded_video.append(video[0])
    for i in video:
        expanded_video.append(i)
    for i in range(add_back_length):
        expanded_video.append(video[-1])

    # RESAMPLE
    offset = longest_frames / frame_target
    index = numpy.arange(0, longest_frames, offset)
    nearest = interp1d(numpy.arange(longest_frames),
                       numpy.arange(longest_frames), kind='nearest')
    for i in nearest(index):
        final_video.append(expanded_video[int(i)])
    return final_video


class ImageLoaderPIL(object):

    def __call__(self, path):
        # open path as file to avoid ResourceWarning (https://github.com/python-pillow/Pillow/issues/835)
        with path.open('rb') as f:
            with Image.open(f) as img:
                return img.convert('RGB')


# class ImageLoaderAccImage(object):

#     def __call__(self, path):
#         import accimage
#         return accimage.Image(str(path))


# Params :
#   video_path
#   frame_indices : Frame index list to load.
# Return :
#   Frame list that contain RGB frames.
class VideoLoader(object):
    def __init__(self, image_name_formatter=None, image_loader=None):
        self.image_name_formatter = image_name_formatter

        if image_loader is None:
            self.image_loader = ImageLoaderPIL()
        else:
            self.image_loader = image_loader

    def __call__(self, video_path, frame_length=20):
        video = []
        # TODO : 길이 제약 삽입하기
        frames = os.listdir(video_path)
        frames.sort()

        for frame in frames:
            image_path = video_path / frame
            if image_path.exists():
                video.append(self.image_loader(image_path))

        n_frames = len(frames)
        video_selected = []
        if n_frames >= frame_length:
            offset = n_frames // frame_length

            for i in range(0, n_frames, offset):
                video_selected.append(video[i])

            # TODO : Imporve this..
            if frame_length < len(video_selected):
                video_selected = video_selected[0:frame_length]

            return video_selected
        else:
            video = [video[0]] * (frame_length - n_frames) + video

        return video


# Params :
#   video_path
#   frame_indices : Frame index list to load.
# Return :
#   Frame list that contain RGB frames.
class FullClipVideoLoader(object):
    def __init__(self, image_name_formatter=None, image_loader=None):
        self.image_name_formatter = image_name_formatter

        if image_loader is None:
            self.image_loader = ImageLoaderPIL()
        else:
            self.image_loader = image_loader

    def __call__(self, video_path, frame_length=20):
        video = []

        frames = os.listdir(video_path)
        frames.sort()
        for frame_name in frames:
            image_path = video_path / frame_name
            if image_path.exists():
                video.append(self.image_loader(image_path))

        n_frame_indices = len(frames)

        if len(video) < frame_length:
            video = [video[0]] * (frame_length - n_frame_indices) + video

        return video
