package main

import (
	"crypto/tls"
	"encoding/binary"
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"os"
	"os/signal"
	"syscall"
	"time"
)

var (
	serverIP       string
	serverPort     string
	localIP        string
	localPort      string
	clientCertPath string
	clientKeyPath  string
	logFilePath    string
	bufferSize     int
	useHTTP        bool
	localHTTPS     bool
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Server IP address")
	flag.StringVar(&serverPort, "server-port", "", "Server port")
	flag.StringVar(&localIP, "local-ip", "127.0.0.1", "Local IP address")
	flag.StringVar(&localPort, "local-port", "80", "Local port")
	flag.StringVar(&clientCertPath, "cert", "", "Path to client certificate")
	flag.StringVar(&clientKeyPath, "key", "", "Path to client key")
	flag.StringVar(&logFilePath, "log", "ssl_tunnel.log", "Path to log file")
	flag.IntVar(&bufferSize, "buffer", 4096, "Buffer size for data transfer")
	flag.BoolVar(&useHTTP, "http", false, "Use HTTP mode for server connection")
	flag.BoolVar(&localHTTPS, "local-https", false, "Use HTTPS for local service connection")
}

func main() {
	flag.Parse()

	if serverIP == "" || serverPort == "" {
		log.Fatal("Server IP and port must be provided. Use -h for help.")
	}

	if !useHTTP && (clientCertPath == "" || clientKeyPath == "") {
		log.Fatal("Client certificate and key must be provided for non-HTTP mode. Use -h for help.")
	}

	// Set up logging
	logFile, err := os.OpenFile(logFilePath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		log.Fatal(err)
	}
	defer logFile.Close()
	log.SetOutput(logFile)

	// Handle graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)
	go func() {
		<-sigChan
		log.Println("Received shutdown signal. Closing tunnel...")
		os.Exit(0)
	}()

	log.Println("Starting SSL tunnel...")

	for {
		err := connectAndForward()
		if err != nil {
			log.Printf("Error: %v\n", err)
			log.Println("Retrying in 5 seconds...")
			time.Sleep(5 * time.Second)
		}
	}
}

func connectAndForward() error {
	var serverConn net.Conn
	var err error

	log.Printf("Attempting to connect to server at %s:%s...", serverIP, serverPort)

	if useHTTP {
		config := &tls.Config{
			InsecureSkipVerify: true,
		}
		serverConn, err = tls.Dial("tcp", fmt.Sprintf("%s:%s", serverIP, serverPort), config)
	} else {
		cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
		if err != nil {
			return fmt.Errorf("failed to load client certificate: %v", err)
		}

		config := &tls.Config{
			Certificates:       []tls.Certificate{cert},
			InsecureSkipVerify: true,
		}
		serverConn, err = tls.Dial("tcp", fmt.Sprintf("%s:%s", serverIP, serverPort), config)
	}

	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer serverConn.Close()

	log.Println("Connected to server successfully.")

	if !useHTTP {
		// Send the initial key for the original protocol
		key := []byte{0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00}
		if _, err = serverConn.Write(key); err != nil {
			return fmt.Errorf("failed to send initial key: %v", err)
		}
		log.Println("Sent initial key.")

		// Receive and parse the response
		response := make([]byte, 20)
		if _, err = io.ReadFull(serverConn, response); err != nil {
			return fmt.Errorf("failed to receive response from server: %v", err)
		}
		log.Printf("Received response from server: %x", response)

		tunnelPort := binary.BigEndian.Uint32(response[16:20])
		log.Printf("Server tunnel port: %d", tunnelPort)
	}

	var localConn net.Conn
	if localHTTPS {
		config := &tls.Config{
			InsecureSkipVerify: true, // Note: In production, you should properly verify the certificate
		}
		localConn, err = tls.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort), config)
	} else {
		localConn, err = net.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort))
	}

	if err != nil {
		return fmt.Errorf("failed to connect to local service: %v", err)
	}
	defer localConn.Close()

	log.Printf("Connected to local service at %s:%s", localIP, localPort)

	// Forward traffic in both directions
	go func() {
		_, err := io.Copy(serverConn, localConn)
		if err != nil {
			log.Printf("Error forwarding local to server: %v", err)
		}
	}()

	_, err = io.Copy(localConn, serverConn)
	if err != nil {
		log.Printf("Error forwarding server to local: %v", err)
	}

	return nil
}