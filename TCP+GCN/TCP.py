import socket
import threading
import time
from GCN_train import GCN_start
from SOM import process_point_cloud  # import at the top
import os

"""
这个脚本是一个 TCP 服务器端，主要用于与 Unity / AR 客户端通信。
它的职责分为四步：
1. 向客户端发送经过 SOM 处理后的点云文件；
2. 接收客户端上传的待处理点云文件；
3. 调用 GCN 进行点云分类/着色；
4. 将生成的着色结果文件发回给客户端。

该代码是典型的“服务端-客户端”循环处理模型，每个新连接都会创建一个线程单独处理。
"""

# 记录菜单状态，用于后续广播消息；当前代码中大多被注释掉了，保留了接口。
menu_status_old = ""
menu_status_new = ""

# 当前连接房间/客户端编号，用于区分不同客户端
roomNum = 0

# 保存当前所有已连接客户端 socket
client_sockets = []

# 服务器监听地址和端口
host = "0.0.0.0"  # 绑定到所有可用网卡，允许局域网客户端访问
port = 2077  # 与 Unity 端约定的端口号，必须一致

# 创建 TCP socket
server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
# 绑定 IP 和端口
server_socket.bind((host, port))

# 开始监听客户端连接，最大排队连接数为 10
server_socket.listen(10)
print("Server started, waiting for client connection...")

# 原始点云文件目录
path_origin = "F:/za/UNITY-SOM"
# 当前要处理的文件名，硬编码为一个固定点云文件
name = "m82D_control1_B_D30_centre_filter.txt"
print(name)

# 中间存储目录：用于保存客户端上传的点云文件
path_tem = "F:/desktop/model_tem/modelnet40-save"
# GCN 结果输出目录：用于保存着色后的点云结果
path_gcn = "F:/desktop/model_gcn/modelnet40-GCN"


def broadcast_message(message):
    """
    向所有已连接客户端广播消息。
    这里是一个扩展接口，当前代码中 menu 相关逻辑被注释掉了，
    因此该函数在当前运行流程中实际上未被使用。
    """
    for client_socket in client_sockets:
        try:
            client_socket.send(message)
        except:
            # 如果发送失败，则移除该 socket
            client_sockets.remove(client_socket)


def handle_client(client_socket, client_address):
    """
    为单个客户端处理完整的请求/响应循环。
    一般流程：
    - 连接成功后先发送处理好的点云文件；
    - 监听客户端消息；
    - 若收到 "file"，则开始接收上传文件并执行 GCN；
    - 若收到 "start-coloring"，则发送 GCN 处理结果文件；
    - 连接断开时清理资源。
    """
    global roomNum

    # 为每个客户端分配一个编号，用于日志和后续扩展
    roomNum += 1
    client_name = roomNum
    print("name", client_name)
    print("client connected:", client_address)
    client_sockets.append(client_socket)

    # 这些全局变量用于菜单/广播状态管理，当前逻辑主要被注释掉
    global menu_status_old, menu_status_new
    menu_status_old = ""
    menu_status_new = ""

    # 发送/接收时用到的特殊字符串，作为文件传输结束标记
    t = "12345"  # 处理后点云文件发送完成时的结束标记
    b = "123456789"  # 颜色结果文件发送完成时的结束标记
    h = "\n"  # 目前未使用

    # 这里的写法是有问题的：os.path.join() 只能拼接路径，不接受 n_som=50, epochs=40 这样的关键字参数。
    # 实际意图很可能是调用 process_point_cloud(..., n_som=50, epochs=40)，也就是先做 SOM 处理。
    # 该行保留为原始代码状态，便于后续修正和调试。
    # processed_file = os.path.join(path_origin, "processed_" + name, n_som=50, epochs=40)
    processed_file = os.path.join(path_origin, "processed_" + name)
    # 对原始点云先进行 SOM 处理，生成处理后的文件
    # process_point_cloud(os.path.join(path_origin, name), processed_file)
    process_point_cloud(
        os.path.join(path_origin, name), processed_file, n_som=50, epochs=40
    )

    # 发送处理后的点云文件给客户端
    with open(processed_file, "rb") as file:
        print("Sending processed point cloud file...")
        while True:
            file_data = file.read(4096)
            if file_data:
                client_socket.send(file_data)
            else:
                # 文件读完后，等待 2 秒，然后发送结束标记，表示本次文件传输结束
                time.sleep(2)
                client_socket.send(t.encode())
                break

    # 主消息循环：处理客户端发来的控制命令和文件上传请求
    while True:
        data = client_socket.recv(1024)

        if not data:
            break

        # 客户端发送 "file" 表示准备上传点云文件
        if data.decode() == "file":
            print("Start receiving file")
            with open(os.path.join(path_tem, name), "wb") as file2:
                while True:
                    file2_data = client_socket.recv(1024 * 1024 * 6)

                    # 当接收到结束标记时，说明上传完成，随后调用 GCN 处理
                    if file2_data.decode() == "OKOKOK":
                        print("Download complete")
                        GCN_start(os.path.join(path_tem, name), name)
                        break
                    if file2_data:
                        file2.write(file2_data)

        # 客户端发送 "start-coloring" 时，服务端读取 GCN 输出结果并发送给客户端
        if data.decode() == "start-coloring":
            print("Start sending colored file")
            time.sleep(2)
            with open(os.path.join(path_gcn, name), "rb") as file1:
                while True:
                    file1_data = file1.read(4096)
                    if file1_data:
                        client_socket.send(file1_data)
                    else:
                        time.sleep(2)
                        print("Transfer complete")
                        client_socket.send(b.encode())
                        break

        # 下面这段是菜单同步的扩展逻辑，当前被注释掉了，说明开发者曾打算实时同步菜单状态。
        # if data.decode().split(":")[0] == "menu":
        #     menu_status_new = data.decode()
        #     content = "menu"+str(client_name)+":"+menu_status_new.split("menu:")[1]
        #     if menu_status_old != menu_status_new:
        #         broadcast_message(content.encode())
        #         menu_status_old = menu_status_new
        #         print("position" + menu_status_old.split(":")[1])
        #         print("rotation" + menu_status_old.split(":")[2])
        #         print("scale" + menu_status_old.split(":")[3])

        if not data:
            break

    # 清理资源，关闭当前客户端连接
    client_sockets.remove(client_socket)
    client_socket.close()
    print("Connection closed:", client_address)


# 主循环：持续监听新的客户端连接，并为每个客户端单独启动线程
while True:
    client_socket, client_address = server_socket.accept()
    client_thread = threading.Thread(
        target=handle_client, args=(client_socket, client_address)
    )
    client_thread.start()
