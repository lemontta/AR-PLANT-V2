from __future__ import division
from __future__ import print_function

import os
import time
import argparse
import numpy as np

import torch
import torch.nn.functional as F
import torch.optim as optim
import pandas as pd
from GCN_utils import load_data, accuracy
from models import GCN


def train(epoch, model, optimizer, features, adj, idx_train, labels, args, idx_val):
    t = time.time()
    model.train()
    optimizer.zero_grad()  # GraphConvolution forward
    output = model(features, adj)  # Run model with input (features, adj)
    l = labels[idx_train].squeeze(0)
    z = output[idx_train].squeeze(0)
    loss_train = F.nll_loss(z, l)
    acc_train = accuracy(z, l)
    loss_train.backward()
    optimizer.step()

    if not args.fastmode:
        # Evaluate validation set performance separately,
        # deactivates dropout during validation run.
        model.eval()
        output = model(features, adj)

    loss_val = F.nll_loss(output[idx_val].squeeze(0), labels[idx_val].squeeze(0))
    acc_val = accuracy(output[idx_val].squeeze(0), labels[idx_val].squeeze(0))
    print(
        "Epoch: {:04d}".format(epoch + 1),
        "loss_train: {:.4f}".format(loss_train.item()),
        "acc_train: {:.4f}".format(acc_train.item()),
        "loss_val: {:.4f}".format(loss_val.item()),
        "acc_val: {:.4f}".format(acc_val.item()),
        "time: {:.4f}s".format(time.time() - t),
    )


def test(model, features, adj, idx_test, labels):
    model.eval()
    output = model(features, adj)
    p = output[idx_test]
    z = idx_test
    loss_test = F.nll_loss(output[idx_test].squeeze(0), labels[idx_test].squeeze(0))
    acc_test = accuracy(output[idx_test].squeeze(0), labels[idx_test].squeeze(0))
    # Each run of the program, the output of labels[idx_test] is different, because in utils.py, the encode_onehot function uses set() to find all classes, and set is unordered (debug); and the true labels in the file are not the labels shown after the program runs, but the result after conversion
    output_labels = output

    print(
        "Test set results:",
        "loss= {:.4f}".format(loss_test.item()),
        "accuracy= {:.4f}".format(acc_test.item()),
    )


def start_tuili(
    model, features, adj, idx_test, labels, features1, plant, labels1, path
):
    model.eval()
    output = model(features, adj)
    p = output[idx_test]
    loss_test = F.nll_loss(output[idx_test].squeeze(0), labels[idx_test].squeeze(0))
    acc_test = accuracy(output[idx_test].squeeze(0), labels[idx_test].squeeze(0))
    print(
        "Test set results:",
        "loss= {:.4f}".format(loss_test.item()),
        "accuracy= {:.4f}".format(acc_test.item()),
    )
    output_labels = output.max(1)[1].tolist()
    labels1_class = sorted(set(labels1))
    if output_labels and max(output_labels) >= len(labels1_class):
        raise ValueError(
            "模型输出类别索引超出标签类别范围: "
            f"max_index={max(output_labels)}, "
            f"class_count={len(labels1_class)}"
        )
    output_labels = [labels1_class[i] for i in output_labels]
    # output_labels = np.array(output_labels)+1
    header = """VERSION .7
FIELDS x y z label object
SIZE 4 4 4 4 4
TYPE F F F U U
COUNT 1 1 1 1 1
WIDTH %d
HEIGHT 1
POINTS %d
VIEWPOINT 0 0 0 1 0 0 0
DATA ascii
""" % (len(labels), len(labels))

    new_data = pd.DataFrame(
        {
            "x": features1[:, 0],
            "y": features1[:, 1],
            "z": features1[:, 2],
            "labels": output_labels,
        }
    )
    new_data.to_csv(
        os.path.join(
            r"F:/desktop/model_gcn/modelnet40-GCN", path.split(".")[0] + ".txt"
        ),
        sep=" ",
        index=False,
        header=False,
    )
    with open(
        os.path.join(
            r"F:/desktop/model_gcn/modelnet40-GCN", path.split(".")[0] + ".txt"
        ),
        "r",
    ) as original:
        data = original.read()
    with open(
        os.path.join(
            r"F:/desktop/model_gcn/modelnet40-GCN", path.split(".")[0] + ".txt"
        ),
        "w",
    ) as modified:
        modified.write(header + data)


def GCN_start(plant, path):

    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--no-cuda", action="store_true", default=False, help="Disables CUDA training."
    )
    parser.add_argument(
        "--fastmode",
        action="store_true",
        default=False,
        help="Validate during training pass.",
    )
    parser.add_argument("--seed", type=int, default=42, help="Random seed.")
    parser.add_argument(
        "--epochs", type=int, default=40, help="Number of epochs to train."
    )
    parser.add_argument("--lr", type=float, default=0.01, help="Initial learning rate.")
    parser.add_argument(
        "--weight_decay",
        type=float,
        default=5e-4,
        help="Weight decay (L2 loss on parameters).",
    )
    parser.add_argument(
        "--hidden", type=int, default=16, help="Number of hidden units."
    )
    parser.add_argument("--nclass", type=int, default=6, help="Number of hidden units.")
    parser.add_argument(
        "--dropout",
        type=float,
        default=0.5,
        help="Dropout rate (1 - keep probability).",
    )

    args = parser.parse_args()
    args.cuda = not args.no_cuda and torch.cuda.is_available()
    Q = args.cuda
    Q1 = args.no_cuda
    Q2 = torch.cuda.is_available()
    np.random.seed(args.seed)
    torch.manual_seed(args.seed)
    if args.cuda:
        torch.cuda.manual_seed(args.seed)
    # MAC: option + command + <-
    # Load data
    (
        adj,
        features1,
        labels,
        idx_train,
        idx_val,
        idx_test,
        nclasses,
        features2,
        labels1,
    ) = load_data(plant)
    v = list(set(labels1))
    # Model and optimizer, build GCN, initialize parameters. Two-layer GCN
    model = GCN(
        nfeat=features1.shape[1],
        nhid=args.hidden,
        nclass=nclasses,
        dropout=args.dropout,
    )
    optimizer = optim.Adam(
        model.parameters(), lr=args.lr, weight_decay=args.weight_decay
    )

    if args.cuda:
        model.cuda()
        features = features1.cuda()
        adj = adj.cuda()
        labels = labels.cuda()
        idx_train = idx_train.cuda()
        idx_val = idx_val.cuda()
        idx_test = idx_test.cuda()

    t_total = time.time()
    for epoch in range(args.epochs):
        train(epoch, model, optimizer, features, adj, idx_train, labels, args, idx_val)
    start_tuili(model, features, adj, idx_test, labels, features2, plant, labels1, path)
