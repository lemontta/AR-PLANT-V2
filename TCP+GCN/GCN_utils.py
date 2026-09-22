import numpy as np
import scipy.sparse as sp
import torch
import pandas as pd
from scipy.sparse import hstack
from scipy import sparse
from sklearn.neighbors import kneighbors_graph



def encode_onehot(labels):
    classes = sorted(set(labels))
    classes_dict = {c: np.identity(len(classes))[i, :] for i, c in
                    enumerate(classes)}
    labels_onehot = np.array(list(map(classes_dict.get, labels)),
                             dtype=np.int32)
    return labels_onehot


def load_data(path):
    print('Loading {} dataset...'.format(path))

    data = pd.read_csv(path, delimiter=' ', header=None,skiprows=10,
                       names=['x', 'y', 'z', 'label','project'])
    idx_features_labels = data.to_numpy(dtype=np.dtype(float))
    np.random.shuffle(idx_features_labels)

    features = sp.csr_matrix(idx_features_labels[:, 0:3], dtype=np.float32)  # Extract features
    features1=idx_features_labels[:,0:3]
    labels = idx_features_labels[:, 3].astype(int)
    zero_indices = np.where(labels == 0)
    non_zero_indices = np.nonzero(labels)
    zero_indices=np.array(zero_indices)
    non_zero_indices=np.array(non_zero_indices)
    classes = sorted(set(labels))
    nclasses = len(classes)
    labels1=labels
    labels = encode_onehot(labels)   # Convert labels to one-hot encoding

    # Build graph
    arr = []
    # Add labels to point cloud data, record which point, split into 3 parts and store in arr array
    for line in features1[0:]:
        xyzARGB = line
        x, y, z = [i for i in xyzARGB[:3]]
        arr.append([x, y, z])
    adj = kneighbors_graph(arr, 500, mode='connectivity', include_self=False)
    adj1 = kneighbors_graph(arr, 500, mode='distance', include_self=False)
    adj1 = adj1 + adj1.T.multiply(adj1.T > adj1) - adj1.multiply(adj1.T > adj1)
    adj1 = normalize_text(adj1+ sp.eye(adj.shape[0]))
    # Build symmetric adjacency matrix
    adj = adj + adj.T.multiply(adj.T > adj) - adj.multiply(adj.T > adj)

    features2=features
    features = hstack([adj, features])
    adj=adj1
    # Convert numpy data to torch format
    features = torch.FloatTensor(np.array(features.todense()))
    features2 = torch.FloatTensor(np.array(features2.todense()))
    labels = torch.LongTensor(np.where(labels)[1])
    adj = sparse_mx_to_torch_sparse_tensor(adj)

    a=non_zero_indices
    b=non_zero_indices

    idx_train = torch.from_numpy(a)
    idx_val = torch.from_numpy(b)
    idx_test = torch.from_numpy(zero_indices)

    return adj, features, labels, idx_train, idx_val, idx_test,nclasses, features2, labels1


def normalize(mx):
    """Row-normalize sparse matrix"""
    rowsum = np.array(mx.sum(1))  # Row sum, actually the degree matrix
    r_inv = np.power(rowsum, -1).flatten()  # Inverse of the sum
    r_inv[np.isinf(r_inv)] = 0.   # If inf, convert to 0
    r_mat_inv = sp.diags(r_inv)  # Construct diagonal matrix
    mx = r_mat_inv.dot(mx)  # Construct D-1*A, asymmetric, simplified
    return mx


def accuracy(output, labels):
    preds = output.max(1)[1].type_as(labels)
    correct = preds.eq(labels).double()
    # Compare predicted class labels with true labels, return boolean tensor, convert to double
    correct = correct.sum()  # Sum all correct predictions
    return correct / len(labels)


def sparse_mx_to_torch_sparse_tensor(sparse_mx):
    """Convert a scipy sparse matrix to a torch sparse tensor."""
    sparse_mx = sparse_mx.tocoo().astype(np.float32)
    indices = torch.from_numpy(
        np.vstack((sparse_mx.row, sparse_mx.col)).astype(np.int64))
    values = torch.from_numpy(sparse_mx.data)
    shape = torch.Size(sparse_mx.shape)
    return torch.sparse.FloatTensor(indices, values, shape)


def CUT_A(mx):
    p=mx
    mx=mx.toarray()
    rowsum = np.array(mx.sum(1))
    row_means=rowsum/20
    mx[mx > row_means[:, None]] = 0
    mx = sp.csr_matrix(mx)
    return mx

def normalize_text(mx):
    mx = np.power(mx.toarray(), -1, where=mx.toarray() != 0)
    mx = sp.csr_matrix(mx)
    mx.data[np.isinf(mx.data)] = 0.
    rowsum = np.array(mx.sum(1))  # Row sum, actually the degree matrix
    r_inv = np.power(rowsum, -1).flatten()  # Inverse of the sum
    r_inv[np.isinf(r_inv)] = 0.   # If inf, convert to 0
    r_mat_inv = sp.diags(r_inv)  # Construct diagonal matrix
    mx = r_mat_inv.dot(mx)  # Construct D-1*A, asymmetric, simplified
    return mx