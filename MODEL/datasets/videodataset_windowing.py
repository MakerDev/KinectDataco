import json
from pathlib import Path
import numpy

import torch
import torch.utils.data as data
from torchvision import transforms
from .loader import VideoLoader


# Params:
#   data : json dict
#   root_path
#   video_path_formatter
# Return :
#   video id list
#   video path list
#   annotation list
#       => annotation is also a dict.


def get_database(data, subset, root_path, video_path_formatter):
    video_ids = []
    video_paths = []
    annotations = []

    for key, value in data['database'].items():
        this_subset = value['subset']
        if this_subset == subset:
            video_ids.append(key)
            annotations.append(value['annotations'])
            if 'video_path' in value:
                video_paths.append(Path(value['video_path']))
            else:
                label = value['annotations']['label']
                video_paths.append(video_path_formatter(root_path, label, key))

    return video_ids, video_paths, annotations


# Map class labels to indices
def get_class_labels(data):
    class_labels_map = {}
    index = 0
    for class_label in data['labels']:
        class_labels_map[class_label] = index
        index += 1
    return class_labels_map


# Params:
#   data : json dict
#   root_path
#   video_path_formatter
# Return :
#   video id list
#   video path list
#   annotation list
#       => annotation is also a dict.
# ■ video_id structure -> pos#_{viewpoint}{participant#}_{pos#}
def generate_database(data, root_path):
    video_ids = []
    video_paths = []
    annotations = []

    # num_participants = data["num_participants"]
    for key, value in data['database'].items():
        video_ids.append(key)
        video_path = Path(root_path / value["video_path"])
        video_paths.append(video_path)
        annotations.append(value["annotations"])

    return video_ids, video_paths, annotations


class WindowingVideoDataset(data.Dataset):
    def __init__(self,
                 root_path,
                 annotation_path,
                 frame_length=15,
                 spatial_transform=None,
                 temporal_transform=None,
                 target_transform=None,
                 video_loader=None,
                 video_path_formatter=(lambda root_path, label, video_id:
                 root_path / label / video_id),
                 image_name_formatter=lambda x: f'image_{x:05d}.jpg',
                 target_type='label'):

        self.dataset, self.class_names = self.__make_dataset(
            root_path, annotation_path, video_path_formatter)

        self.frame_length = frame_length
        self.spatial_transform = spatial_transform
        self.temporal_transform = temporal_transform
        self.target_transform = target_transform

        if video_loader is None:
            self.clip_loader = VideoLoader(image_name_formatter)
        else:
            self.clip_loader = video_loader

        self.target_type = target_type

    def __make_dataset(self, root_path, annotation_path,
                       video_path_formatter=None):

        with annotation_path.open('r') as f:
            data = json.load(f)

        # video_ids, video_paths, annotations = get_database(
        #     data, subset, root_path, video_path_formatter)
        video_ids, video_paths, annotations = generate_database(
            data, root_path)

        # key : class name, value : idx
        class_to_idx = get_class_labels(data)
        idx_to_class = {}
        for name, label in class_to_idx.items():
            idx_to_class[label] = name

        n_videos = len(video_ids)
        dataset = []
        for i in range(n_videos):
            if (i + 1) % 20 == 0:
                print('dataset loading [{}/{}]'.format(i + 1, len(video_ids)))

            if 'label' in annotations[i]:
                label = annotations[i]['label']
                label_id = class_to_idx[label]
            else:
                label = 'test'
                label_id = -1

            video_path = video_paths[i]

            if not video_path.exists():
                continue

            segment = annotations[i]['segment']

            if segment[0] == -1:
                continue

            sample = {
                'video': video_path,
                'segment': segment,
                'frame_indices': segment,
                'video_id': video_ids[i],
                'label': label_id
            }
            dataset.append(sample)

        return dataset, idx_to_class

    # Load clip and convert the loaded frame list to a 4D tensor.
    def __loading(self, path, frame_indices):
        clip = self.clip_loader(path, frame_indices, self.frame_length)

        tf = transforms.ToTensor()
        if self.spatial_transform is not None:
            self.spatial_transform.randomize_parameters()
            clip = [self.spatial_transform(img) for img in clip]
        else:
            clip = [tf(img) for img in clip]
        # Use stack to make list as 4D tensor -> time x RGB

        clip = torch.stack(clip, 0).permute(1, 0, 2, 3)

        return clip

    # Returns a clip and target label. Clip is converted to Tensor
    def __getitem__(self, index):
        # TODO : data에 데이터 베이스 정보를 넣어둬야함.
        path = self.dataset[index]['video']

        if isinstance(self.target_type, list):
            target = [self.dataset[index][t] for t in self.target_type]
        else:
            target = self.dataset[index][self.target_type]

        frame_indices = self.dataset[index]['frame_indices']
        if self.temporal_transform is not None:
            frame_indices = self.temporal_transform(frame_indices)

        clip = self.__loading(path, frame_indices)

        if self.target_transform is not None:
            target = self.target_transform(target)

        return clip, target

    def __len__(self):
        return len(self.dataset)
