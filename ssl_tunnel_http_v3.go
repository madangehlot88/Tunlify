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
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Server IP address")
	flag.StringVar(&serverPort, "server-port", "", "Server port")
	flag.StringVar(&localIP, "local-ip", "", "Local IP address")
	flag.StringVar(&localPort, "local-port", "", "Local port")
	flag.StringVar(&clientCertPath, "cert", "", "Path to client certificate")
	flag.StringVar(&clientKeyPath, "key", "", "Path to client key")
	flag.StringVar(&logFilePath, "log", "ssl_tunnel.log", "Path to log file")
	flag.IntVar(&bufferSize, "buffer", 4096, "Buffer size for data transfer")
	flag.BoolVar(&useHTTP, "http", false, "Use HTTP/HTTPS instead of raw TCP")
}

func main() {
	flag.Parse()

	if serverIP == "" || serverPort == "" || localIP == "" || localPort == "" {
		log.Fatal("Server IP, server port, local IP, and local port must be provided. Use -h for help.")
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

	if useHTTP {
		config := &tls.Config{
			InsecureSkipVerify: true,
		}
		serverConn, err = tls.Dial("tcp", serverIP+":"+serverPort, config)
	} else {
		cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
		if err != nil {
			return fmt.Errorf("failed to load client certificate: %v", err)
		}

		config := &tls.Config{
			Certificates:       []tls.Certificate{cert},
			InsecureSkipVerify: true,
			MinVersion:         tls.VersionTLS12,
			MaxVersion:         tls.VersionTLS13,
		}

		serverConn, err = tls.Dial("tcp", serverIP+":"+serverPort, config)
	}

	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer serverConn.Close()

	log.Println("Connected to server.")

	if !useHTTP {
		// ... (original protocol handling remains the same)
	} else {
		localConn, err := net.Dial("tcp", fmt.Sprintf("%s:%s", localIP, localPort))
		if err != nil {
			return fmt.Errorf("failed to connect to local service: %v", err)
		}
		defer localConn.Close()

		log.Printf("Connected to local service at %s:%s", localIP, localPort)

		// Forward traffic in both directions
		go io.Copy(serverConn, localConn)
		io.Copy(localConn, serverConn)
	}

	return nil
}