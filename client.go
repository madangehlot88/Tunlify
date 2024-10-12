package main

import (
	"bufio"
	"fmt"
	"io"
	"net"
	"net/http"
)

const (
	localAddr  = "localhost:5000"
	serverAddr = "167.71.227.50:8001"
)

func main() {
	conn, err := net.Dial("tcp", serverAddr)
	if err != nil {
		fmt.Println("Error connecting to server:", err)
		return
	}
	defer conn.Close()

	fmt.Println("Connected to server")

	for {
		handleRequest(conn)
	}
}

func handleRequest(conn net.Conn) {
	// Read request from server
	req, err := http.ReadRequest(bufio.NewReader(conn))
	if err != nil {
		if err != io.EOF {
			fmt.Println("Error reading request:", err)
		}
		return
	}

	// Forward request to local server
	localReq, err := http.NewRequest(req.Method, "http://"+localAddr+req.URL.Path, req.Body)
	if err != nil {
		fmt.Println("Error creating local request:", err)
		return
	}
	localReq.Header = req.Header

	resp, err := http.DefaultClient.Do(localReq)
	if err != nil {
		fmt.Println("Error forwarding request:", err)
		return
	}
	defer resp.Body.Close()

	// Send response back to server
	err = resp.Write(conn)
	if err != nil {
		fmt.Println("Error writing response:", err)
		return
	}
}