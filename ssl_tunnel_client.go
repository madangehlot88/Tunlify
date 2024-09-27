package main

import (
        "crypto/tls"
        "encoding/binary"
        "fmt"
        "io"
        "net"
        "os"
        "time"
)

func main() {
        if len(os.Args) != 7 {
                fmt.Println("Usage: ./ssl_tunnel <ServerIp> <ServerPort> <LocalIp> <LocalPort> <ClientCertPath> <ClientKeyPath>")
                os.Exit(1)
        }

        serverIP := os.Args[1]
        serverPort := os.Args[2]
        localIP := os.Args[3]
        localPort := os.Args[4]
        clientCertPath := os.Args[5]
        clientKeyPath := os.Args[6]

        for {
                err := connectAndForward(serverIP, serverPort, localIP, localPort, clientCertPath, clientKeyPath)
                if err != nil {
                        fmt.Printf("Error: %v\n", err)
                        fmt.Println("Retrying in 5 seconds...")
                        time.Sleep(5 * time.Second)
                }
        }
}

func connectAndForward(serverIP, serverPort, localIP, localPort, clientCertPath, clientKeyPath string) error {
        cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
        if err != nil {
                return fmt.Errorf("failed to load client certificate: %v", err)
        }

        config := &tls.Config{
                Certificates:       []tls.Certificate{cert},
                InsecureSkipVerify: true,
        }
        conn, err := tls.Dial("tcp", serverIP+":"+serverPort, config)
        if err != nil {
                return fmt.Errorf("failed to connect to server: %v", err)
        }
        defer conn.Close()

        fmt.Println("Connected to server and SSL handshake completed.")

        key := []byte{0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00}
        if _, err = conn.Write(key); err != nil {
                return fmt.Errorf("failed to send initial key: %v", err)
        }
        fmt.Println("Sent initial key.")

        response := make([]byte, 20)
        if _, err = io.ReadFull(conn, response); err != nil {
                return fmt.Errorf("failed to receive response from server: %v", err)
        }
        fmt.Printf("Received response from server: %x\n", response)

        receivedServerPort := binary.BigEndian.Uint32(response[16:20])
        fmt.Printf("Server is listening on port: %d\n", receivedServerPort)

        localConn, err := net.Dial("tcp", localIP+":"+localPort)
        if err != nil {
                return fmt.Errorf("failed to connect to local endpoint: %v", err)
        }
        defer localConn.Close()

        fmt.Println("Connected to local endpoint. Forwarding traffic...")

        errChan := make(chan error, 2)
        go forward(conn, localConn, "Server -> Local", errChan)
        go forward(localConn, conn, "Local -> Server", errChan)

        return <-errChan
}

func forward(src, dst net.Conn, direction string, errChan chan<- error) {
        _, err := io.Copy(dst, src)
        if err != nil {
                errChan <- fmt.Errorf("%s forwarding error: %v", direction, err)
        }
}