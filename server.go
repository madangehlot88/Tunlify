package main

import (
        "bufio"
        "fmt"
        "io"
        "net"
        "net/http"
)

const (
        publicAddr = ":8000"
        tunnelAddr = ":8001"
)

func main() {
        // Start tunnel listener
        tunnelListener, err := net.Listen("tcp", tunnelAddr)
        if err != nil {
                fmt.Println("Error starting tunnel listener:", err)
                return
        }

        // Accept tunnel connection
        tunnelConn, err := tunnelListener.Accept()
        if err != nil {
                fmt.Println("Error accepting tunnel connection:", err)
                return
        }
        defer tunnelConn.Close()

        fmt.Println("Tunnel client connected")

        // Start HTTP server
        http.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
                handleRequest(tunnelConn, w, r)
        })

        fmt.Println("Starting HTTP server on", publicAddr)
        http.ListenAndServe(publicAddr, nil)
}

func handleRequest(tunnelConn net.Conn, w http.ResponseWriter, r *http.Request) {
        // Forward request to tunnel client
        err := r.Write(tunnelConn)
        if err != nil {
                fmt.Println("Error forwarding request:", err)
                http.Error(w, "Internal Server Error", http.StatusInternalServerError)
                return
        }

        // Read response from tunnel client
        resp, err := http.ReadResponse(bufio.NewReader(tunnelConn), r)
        if err != nil {
                fmt.Println("Error reading response:", err)
                http.Error(w, "Internal Server Error", http.StatusInternalServerError)
                return
        }
        defer resp.Body.Close()

        // Copy headers
        for k, v := range resp.Header {
                w.Header()[k] = v
        }

        // Set status code
        w.WriteHeader(resp.StatusCode)

        // Copy body
        _, err = io.Copy(w, resp.Body)
        if err != nil {
                fmt.Println("Error copying response body:", err)
        }
}